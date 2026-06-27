using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using DynLock.Addin.Dynamo;
using DynLock.Core;
using Form = System.Windows.Forms.Form;
using Control = System.Windows.Forms.Control;
using ComboBox = System.Windows.Forms.ComboBox;
using TextBox = System.Windows.Forms.TextBox;
using Panel = System.Windows.Forms.Panel;
using Color = System.Drawing.Color;

namespace DynLock.Addin.UI
{
    /// <summary>
    /// Dynamic input form for a decrypted graph.
    /// Selection inputs are edited here too so the user sees all inputs first.
    /// </summary>
    public class InputForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        private readonly UIApplication _uiApp;
        private readonly DynInputPatcher _patcher;
        private readonly List<RowState> _rows = new List<RowState>();
        private readonly Dictionary<DynInputItem, List<string>> _pickedIds;
        private bool _selectionUsedVisibleShell;
        private double _selectionSavedOpacity = 1.0;
        private FormWindowState _selectionSavedWindowState = FormWindowState.Normal;

        private sealed class RowState
        {
            public DynInputItem Item;
            public Control Editor;
            public TextBox StatusBox;
        }

        private sealed class ComboValue
        {
            public string Display { get; set; }
            public string Value { get; set; }

            public override string ToString()
            {
                return Display ?? Value ?? "";
            }
        }

        public InputForm(UIApplication uiApp, DynInputPatcher patcher)
        {
            _uiApp = uiApp;
            _patcher = patcher;
            _pickedIds = new Dictionary<DynInputItem, List<string>>();
            BuildUi();
        }

        private void BuildUi()
        {
            Text = _patcher.GraphName;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoScaleMode = AutoScaleMode.Dpi;
            Width = 700;
            BackColor = Color.White;

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = Color.FromArgb(18, 90, 175),
            };

            var iconWrap = new Panel { Bounds = new System.Drawing.Rectangle(16, 12, 44, 44), BackColor = Color.White };
            var logoBox = new PictureBox
            {
                Image = AddinIcons.HeaderIcon(36),
                SizeMode = PictureBoxSizeMode.Zoom,
                Bounds = new System.Drawing.Rectangle(4, 4, 36, 36),
                BackColor = Color.White,
            };
            iconWrap.Controls.Add(logoBox);

            var titleLabel = new Label
            {
                Text = "BIMLab Player",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new System.Drawing.Point(70, 14),
            };
            var subtitleLabel = new Label
            {
                Text = _patcher.GraphName,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(160, 200, 255),
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new System.Drawing.Point(72, 40),
            };

            header.Controls.AddRange(new Control[] { iconWrap, titleLabel, subtitleLabel });

            var sep1 = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = Color.FromArgb(210, 218, 236),
            };

            var tableHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.White,
                Padding = new Padding(12),
            };

            var table = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                AutoSize = true,
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            foreach (var item in _patcher.Inputs)
            {
                if (item.Kind == InputKind.Unsupported)
                    continue;

                var label = new Label
                {
                    Text = item.Name,
                    AutoSize = true,
                    Anchor = AnchorStyles.Left,
                    Margin = new Padding(3, 10, 3, 3),
                };

                Control editor = CreateEditor(item, out TextBox statusBox);
                table.RowCount++;
                table.Controls.Add(label);
                table.Controls.Add(editor);
                _rows.Add(new RowState
                {
                    Item = item,
                    Editor = editor,
                    StatusBox = statusBox,
                });
            }

            tableHost.Controls.Add(table);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 54,
                Padding = new Padding(12, 8, 12, 8),
                BackColor = Color.FromArgb(244, 246, 251),
            };
            var sepBtn = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(210, 218, 236) };

            var cancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel };
            var ok = new Button { Text = ">  Chạy" };
            StyleBtn(cancel, Color.FromArgb(218, 226, 242), Color.FromArgb(52, 72, 108), 88, 38);
            StyleBtn(ok, Color.FromArgb(34, 126, 68), Color.White, 110, 38);
            ok.Click += (s, e) => OnRunClicked();
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);

            Controls.Add(tableHost);
            Controls.Add(sep1);
            Controls.Add(header);
            Controls.Add(buttons);
            Controls.Add(sepBtn);

            AcceptButton = ok;
            CancelButton = cancel;
            Height = 760;
        }

        private static void StyleBtn(Button b, Color bg, Color fg, int w, int h)
        {
            b.Width = w;
            b.Height = h;
            b.BackColor = bg;
            b.ForeColor = fg;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.BorderColor = bg;
            b.Font = new Font("Segoe UI", 9.5f);
            b.Cursor = Cursors.Hand;
        }

        private Control CreateEditor(DynInputItem item, out TextBox statusBox)
        {
            statusBox = null;

            switch (item.Kind)
            {
                case InputKind.Text:
                    return new TextBox
                    {
                        Text = item.CurrentText,
                        Dock = DockStyle.Fill,
                        Margin = new Padding(3, 6, 3, 3),
                    };

                case InputKind.Number:
                    return new TextBox
                    {
                        Text = item.CurrentText,
                        Dock = DockStyle.Fill,
                        Margin = new Padding(3, 6, 3, 3),
                    };

                case InputKind.Bool:
                    return new CheckBox
                    {
                        Checked = string.Equals(item.CurrentText, "true", StringComparison.OrdinalIgnoreCase),
                        Margin = new Padding(3, 9, 3, 3),
                    };

                case InputKind.CategoryDropdown:
                    return MakeCategoryCombo(item);

                case InputKind.LevelDropdown:
                    return MakeCombo(item, GetLevelNames());

                case InputKind.FamilyTypeDropdown:
                    return MakeCombo(item, GetFamilyTypeNames());

                case InputKind.GenericDropdown:
                    return MakeEditableCombo(item);

                case InputKind.ElementSelection:
                case InputKind.ElementsSelection:
                    return MakeSelectionEditor(item, out statusBox);

                case InputKind.FilePath:
                    return MakePathEditor(item, true);

                case InputKind.DirectoryPath:
                    return MakePathEditor(item, false);

                default:
                return new Label { Text = "(giữ giá trị đã lưu)" };
            }
        }

        private ComboBox MakeCombo(DynInputItem item, List<string> values)
        {
            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 6, 3, 3),
            };
            combo.Items.AddRange(values.Cast<object>().ToArray());
            int idx = values.IndexOf(item.CurrentText);
            if (idx >= 0) combo.SelectedIndex = idx;
            else if (combo.Items.Count > 0) combo.SelectedIndex = 0;
            return combo;
        }

        private ComboBox MakeCategoryCombo(DynInputItem item)
        {
            var values = GetCategoryValues();
            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 6, 3, 3),
            };
            combo.Items.AddRange(values.Cast<object>().ToArray());

            int idx = values.FindIndex(v =>
                string.Equals(v.Value, item.CurrentText, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(v.Display, item.CurrentText, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) combo.SelectedIndex = idx;
            else if (combo.Items.Count > 0) combo.SelectedIndex = 0;
            return combo;
        }

        private ComboBox MakeEditableCombo(DynInputItem item)
        {
            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 6, 3, 3),
                Text = item.CurrentText,
            };

            if (!string.IsNullOrWhiteSpace(item.CurrentText))
                combo.Items.Add(item.CurrentText);
            return combo;
        }

        private Control MakeSelectionEditor(DynInputItem item, out TextBox statusBox)
        {
            if (IsCadSelection(item))
                return MakeCadSelectionEditor(item, out statusBox);

            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 34,
                Margin = new Padding(3, 6, 3, 3),
            };

            var summaryBox = new TextBox
            {
                ReadOnly = true,
                Multiline = true,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(245, 247, 252),
                Text = SelectionSummary(item),
            };

            var button = new Button
            {
                Text = "Chọn...",
                Dock = DockStyle.Right,
                Width = 88,
            };
            StyleBtn(button, Color.FromArgb(30, 120, 215), Color.White, 88, 34);
            button.Click += (_, __) => PickSelection(item, summaryBox);

            statusBox = summaryBox;
            panel.Controls.Add(summaryBox);
            panel.Controls.Add(button);
            return panel;
        }

        private Control MakeCadSelectionEditor(DynInputItem item, out TextBox statusBox)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 26,
                Margin = new Padding(3, 6, 3, 3),
            };

            var manualButton = new Button
            {
                Text = "Chọn...",
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Width = 78,
                Height = 24,
            };
            StyleBtn(manualButton, Color.FromArgb(30, 120, 215), Color.White, 78, 24);

            var summaryBox = new TextBox
            {
                ReadOnly = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Height = 24,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(245, 247, 252),
                Text = "Chưa có CAD",
            };

            Action layoutCadRow = () =>
            {
                manualButton.Location = new System.Drawing.Point(Math.Max(0, panel.ClientSize.Width - manualButton.Width), 0);
                summaryBox.Location = new System.Drawing.Point(0, 1);
                summaryBox.Width = Math.Max(0, panel.ClientSize.Width - manualButton.Width);
            };
            panel.Resize += (_, __) => layoutCadRow();
            manualButton.Click += (_, __) => PickCadSelection(item, summaryBox);

            panel.Controls.Add(summaryBox);
            panel.Controls.Add(manualButton);
            layoutCadRow();

            statusBox = summaryBox;
            return panel;
        }

        private Control MakePathEditor(DynInputItem item, bool file)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 34,
                Margin = new Padding(3, 6, 3, 3),
            };

            var text = new TextBox
            {
                Text = item.CurrentText,
                Dock = DockStyle.Fill,
            };

            var button = new Button
            {
                Text = file ? "File..." : "Folder...",
                Dock = DockStyle.Right,
                Width = 88,
            };
            StyleBtn(button, Color.FromArgb(30, 120, 215), Color.White, 88, 34);
            button.Click += (_, __) =>
            {
                if (file)
                {
                    using (var dlg = new OpenFileDialog { Title = item.Name })
                    {
                        if (dlg.ShowDialog() == DialogResult.OK)
                            text.Text = dlg.FileName;
                    }
                }
                else
                {
                    using (var dlg = new FolderBrowserDialog { Description = item.Name })
                    {
                        if (dlg.ShowDialog() == DialogResult.OK)
                            text.Text = dlg.SelectedPath;
                    }
                }
            };

            panel.Controls.Add(text);
            panel.Controls.Add(button);
            return panel;
        }

        private string SelectionSummary(DynInputItem item)
        {
            if (_pickedIds.TryGetValue(item, out var ids) && ids.Count > 0)
                return IsCadSelection(item)
                    ? ids.Count == 1 ? "Đã chọn CAD" : "Đã chọn CAD (" + ids.Count + ")"
                    : "OK Đã chọn " + ids.Count + " đối tượng";

            if (item.IsSelection)
                return IsCadSelection(item) ? "Chưa có CAD" : "Chưa chọn";

            string current = item.CurrentText;
            if (!string.IsNullOrWhiteSpace(current))
                return current;

            return "Chưa chọn";
        }

        private void PickSelection(DynInputItem item, TextBox statusBox)
        {
            var uidoc = _uiApp.ActiveUIDocument;
            var doc = uidoc.Document;
            bool oldShowInTaskbar = ShowInTaskbar;
            bool oldTopMost = TopMost;

            try
            {
                // Revit selection cannot receive clicks while a modal WinForms dialog is on top.
                HideInputFormForSelection();
                ActivateRevitForSelection();

                var ids = new List<string>();
                if (item.Kind == InputKind.ElementsSelection)
                {
                    IList<Reference> refs = uidoc.Selection.PickObjects(
                        ObjectType.Element, "Chọn cho: " + item.Name);
                    ids.AddRange(refs.Select(r => doc.GetElement(r).UniqueId));
                }
                else
                {
                    Reference r = uidoc.Selection.PickObject(
                        ObjectType.Element, "Chọn cho: " + item.Name);
                    ids.Add(doc.GetElement(r).UniqueId);
                }

                if (ids.Count > 0)
                    _pickedIds[item] = ids;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User cancelled.
            }
            finally
            {
                if (statusBox != null)
                    statusBox.Text = SelectionSummary(item);

                RestoreInputFormAfterSelection(oldShowInTaskbar, oldTopMost);
            }
        }

        private void PickCadSelection(DynInputItem item, TextBox statusBox)
        {
            PickCadWithFinish(item, statusBox);
        }

        private void PickCadWithFinish(DynInputItem item, TextBox statusBox)
        {
            var uidoc = _uiApp.ActiveUIDocument;
            var doc = uidoc.Document;
            bool oldShowInTaskbar = ShowInTaskbar;
            bool oldTopMost = TopMost;

            try
            {
                HideInputFormForSelection(keepVisibleShell: ShouldKeepVisibleShellForSelection());
                ActivateRevitForSelection();

                IList<Reference> references = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    "Quét/chọn CAD trong Revit, sau đó bấm Finish.");

                var ids = references
                    .Select(r => doc.GetElement(r))
                    .Where(e => e != null)
                    .Select(e => e.UniqueId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (item.Kind == InputKind.ElementSelection && ids.Count > 1)
                    ids = ids.Take(1).ToList();

                if (ids.Count > 0)
                    _pickedIds[item] = ids;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User cancelled selection.
            }
            finally
            {
                if (statusBox != null)
                    statusBox.Text = SelectionSummary(item);

                RestoreInputFormAfterSelection(oldShowInTaskbar, oldTopMost);
            }
        }

        private bool ShouldKeepVisibleShellForSelection()
        {
            string version = _uiApp?.Application?.VersionNumber;
            return int.TryParse(version, out int year) && year <= 2024;
        }

        private void HideInputFormForSelection(bool keepVisibleShell = false)
        {
            _selectionUsedVisibleShell = keepVisibleShell;
            _selectionSavedOpacity = Opacity;
            _selectionSavedWindowState = WindowState;
            Enabled = false;
            TopMost = false;
            ShowInTaskbar = false;
            if (keepVisibleShell)
            {
                WindowState = FormWindowState.Normal;
                Opacity = 0.02;
            }
            else
            {
                Hide();
            }
            Application.DoEvents();
        }

        private void RestoreInputFormAfterSelection(bool oldShowInTaskbar, bool oldTopMost)
        {
            if (IsDisposed)
                return;

            Action restore = () =>
            {
                if (IsDisposed)
                    return;

                ForceShowInputForm(oldShowInTaskbar, oldTopMost);
                Application.DoEvents();

                // Revit 2024 can keep focus after PickObjects + Finish. Retry briefly so
                // the input form reliably comes back for the remaining parameters.
                var retryTimer = new Timer { Interval = 150 };
                int ticks = 0;
                retryTimer.Tick += (_, __) =>
                {
                    if (IsDisposed)
                    {
                        retryTimer.Stop();
                        retryTimer.Dispose();
                        return;
                    }

                    ticks++;
                    ForceShowInputForm(oldShowInTaskbar, ticks < 4 || oldTopMost);

                    if (ticks >= 6)
                    {
                        TopMost = oldTopMost;
                        retryTimer.Stop();
                        retryTimer.Dispose();
                    }
                };
                retryTimer.Start();
            };

            if (InvokeRequired)
                BeginInvoke(restore);
            else
                restore();
        }

        private void ForceShowInputForm(bool showInTaskbar, bool topMost)
        {
            if (IsDisposed)
                return;

            ShowInTaskbar = showInTaskbar;
            Opacity = _selectionSavedOpacity;
            Enabled = true;
            WindowState = _selectionSavedWindowState;
            Show();
            ShowWindow(Handle, SW_RESTORE);
            TopMost = topMost;
            BringToFront();
            Focus();
            Activate();
            SetForegroundWindow(Handle);
        }

        private void ActivateRevitForSelection()
        {
            IntPtr revitHandle = _uiApp.MainWindowHandle;
            if (revitHandle == IntPtr.Zero)
                return;

            // ShowDialog disables Revit's main window. Re-enable it before calling Revit selection.
            EnableWindow(revitHandle, true);
            if (IsIconic(revitHandle))
                return;
            SetForegroundWindow(revitHandle);
            Application.DoEvents();
        }

        private bool IsCadSelection(DynInputItem item)
        {
            if (item == null || !item.IsSelection)
                return false;

            string name = item.Name ?? "";
            return name.IndexOf("CAD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("DWG", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Link", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void OnRunClicked()
        {
            foreach (var row in _rows)
            {
                var item = row.Item;
                var control = row.Editor;

                switch (item.Kind)
                {
                    case InputKind.Text:
                    case InputKind.FilePath:
                    case InputKind.DirectoryPath:
                        item.SetValue(GetEditorText(control));
                        break;

                    case InputKind.Number:
                        string raw = ((TextBox)control).Text.Trim().Replace(',', '.');
                        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
                        {
                            MessageBox.Show($"\"{item.Name}\" phải là số.", "BIMLab Player",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        item.SetNumber(num);
                        break;

                    case InputKind.Bool:
                        item.SetBool(((CheckBox)control).Checked);
                        break;

                    case InputKind.CategoryDropdown:
                        var categoryCombo = (ComboBox)control;
                        if (categoryCombo.SelectedItem is ComboValue category)
                            item.SetDropdown(category.Value);
                        break;

                    case InputKind.LevelDropdown:
                    case InputKind.FamilyTypeDropdown:
                        var combo = (ComboBox)control;
                        if (combo.SelectedItem != null)
                            item.SetDropdown((string)combo.SelectedItem);
                        break;

                    case InputKind.GenericDropdown:
                        item.SetDropdown(((ComboBox)control).Text);
                        break;

                    case InputKind.ElementSelection:
                    case InputKind.ElementsSelection:
                        if (_pickedIds.TryGetValue(item, out var ids) && ids.Count > 0)
                        {
                            item.SetSelection(ids);
                            break;
                        }

                        MessageBox.Show($"Vui lòng chọn \"{item.Name}\" trước khi chạy.",
                            "BIMLab Player", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                }
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private List<string> GetLevelNames()
        {
            return new FilteredElementCollector(_uiApp.ActiveUIDocument.Document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .Select(l => l.Name)
                .ToList();
        }

        private List<ComboValue> GetCategoryValues()
        {
            return Enum.GetNames(typeof(BuiltInCategory))
                .Where(n => n.StartsWith("OST_", StringComparison.Ordinal))
                .Select(n => new ComboValue
                {
                    Value = n,
                    Display = CategoryDisplayName(n),
                })
                .Distinct()
                .OrderBy(n => n.Display, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string CategoryDisplayName(string builtInCategoryName)
        {
            if (string.IsNullOrWhiteSpace(builtInCategoryName))
                return "";

            try
            {
                var bic = (BuiltInCategory)Enum.Parse(typeof(BuiltInCategory), builtInCategoryName);
                string label = LabelUtils.GetLabelFor(bic);
                if (!string.IsNullOrWhiteSpace(label))
                    return label;
            }
            catch
            {
                // Some BuiltInCategory values do not have a user-facing Revit label.
            }

            string name = builtInCategoryName.StartsWith("OST_", StringComparison.Ordinal)
                ? builtInCategoryName.Substring(4)
                : builtInCategoryName;

            return SplitPascalCase(name);
        }

        private static string SplitPascalCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "";

            var chars = new List<char>();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c) && !char.IsWhiteSpace(value[i - 1]) && !char.IsUpper(value[i - 1]))
                    chars.Add(' ');
                chars.Add(c == '_' ? ' ' : c);
            }

            return new string(chars.ToArray()).Trim();
        }

        private List<string> GetFamilyTypeNames()
        {
            return new FilteredElementCollector(_uiApp.ActiveUIDocument.Document)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Select(fs => fs.FamilyName + ":" + fs.Name)
                .Distinct()
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string GetEditorText(Control control)
        {
            if (control is TextBox textBox)
                return textBox.Text;

            return FindChildTextBox(control)?.Text ?? "";
        }

        private static TextBox FindChildTextBox(Control control)
        {
            if (control == null) return null;
            if (control is TextBox textBox) return textBox;

            foreach (Control child in control.Controls)
            {
                var found = FindChildTextBox(child);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}

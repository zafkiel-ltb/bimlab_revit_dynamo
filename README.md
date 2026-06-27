# DynLock - Chạy script Dynamo ma hoa trong Revit

Bo cong cu cho phep **team lead** ma hoa file `.dyn` thanh `.dynx`, va **nhan vien**
chay script do tu mot nut tren ribbon Revit **ma khong bao gio nhin thay node hay
thong so ben trong** (Dynamo chay ngam, khong mo cua so).

> Luu y nguyen tac: khong the tao file ma Dynamo *mo duoc tren canvas* nhung
> *khong xem duoc*. Giai phap nay an noi dung bang cach **khong bao gio mo
> Dynamo UI** - script duoc giai ma trong bo nho va chay headless.

## Thanh phan

| Project | Vai tro | Ai dung |
|---|---|---|
| `DynLock.Core` | Thu vien ma hoa AES-256 + HMAC dung chung | (build chung) |
| `DynLock.AuthServer` | Server HTTP noi bo, luu database local `auth.db` | May chu/LAN |
| `DynLock.Encryptor` | `DynLockEncrypt.exe` - chuyen `.dyn` -> `.dynx` | Team lead |
| `DynLock.Addin` | Addin Revit: nut **BIMLab -> Run Tool**, form nhap thong so, chay ngam | Nhan vien |

Target hien tai: **Revit 2024 / Dynamo 2.19 / .NET Framework 4.8** (khop file
`FILE DYNAMO GUI TEAM IOT.dyn`). Addin nap `DynamoRevitDS.dll` bang reflection
nen thuong chay duoc tren 2021-2024 ma khong can doi code; voi Revit 2025+ can
doi TargetFramework sang `net8.0-windows` va package API sang `2025.*`/`2026.*`.

## Auth server noi bo

Quyen truy cap khong con phu thuoc Supabase. Chay `DynLock.AuthServer` tren mot
may trong LAN; server nay luu database local tai:

```text
C:\ProgramData\BIMLab\DynLock\auth.db
```

Tao file cau hinh tren may server va cac may client:

```json
{
  "AuthServerUrl": "http://192.168.1.50:5050",
  "SuperAdminEmail": "admin@company.com"
}
```

Duong dan file:

```text
C:\ProgramData\BIMLab\DynLock\authserver.json
```

Hien tai co the de `AuthServerUrl` tro ve IP may nay. Sau nay neu dua database/API
len server that, chi can doi `AuthServerUrl` thanh IP hoac domain moi, vi du
`https://bimlab-auth.company.vn`.

Chay server tren may host:

```powershell
.\run_auth_server.ps1
```

Neu muon doi cong:

```powershell
.\run_auth_server.ps1 -BindUrl "http://0.0.0.0:6060"
```

Import du lieu leader tu Supabase cu sang database local mot lan:

```powershell
.\import_supabase_to_local.ps1 `
  -SuperAdminEmail "admin@company.com" `
  -LegacySupabaseUrl "https://your-project.supabase.co" `
  -LegacySupabaseAnonKey "<old-supabase-anon-key>" `
  -AuthServerUrl "http://192.168.1.50:5050"
```

Sau khi import, chay server va kiem tra:

```powershell
Invoke-RestMethod "http://192.168.1.50:5050/api/health"
Invoke-RestMethod "http://192.168.1.50:5050/api/auth/check?email=leader@gmail.com"
```

## Team lead can lam gi (checklist)

1. **Build** (can may Windows + Visual Studio 2022 hoac .NET SDK):
   ```
   dotnet build DynLock.sln -c Release
   ```
2. **Ma hoa script**:
   ```
   DynLockEncrypt.exe "FILE DYNAMO GUI TEAM IOT.dyn"
   ```
   -> tao `FILE DYNAMO GUI TEAM IOT.dynx`. Xóa/cat file `.dyn` goc, chi phat `.dynx`.
3. **Cai addin cho nhan vien** (moi may):
   - Cach de nhat: chay `Cai dat BIMLab DynLock.exe` (project `DynLock.Installer`).
   - Hoac tay: copy `DynLock.Addin.dll` + `DynLock.Core.dll` + `Newtonsoft.Json.dll`
     vao `C:\ProgramData\BIMLab\DynLock\`, copy `install\DynLock.addin` vao
     `C:\ProgramData\Autodesk\Revit\Addins\2024\`.
   - **Package Dynamo**: addin tu go cac node package "mo coi" (khong noi day)
     khoi graph truoc khi chay - graph hien tai nho do **khong can bimorphNodes**
     nua (node `CAD.CurvesFromCADLayers` da duoc thay bang Python node va chi con
     sot lai khong noi day). Neu mot cong cu khac *thuc su* dung node package,
     addin se tu bao ten package con thieu cho nhan vien truoc khi chay
     (package cai trong `%AppData%\Dynamo\Dynamo Revit\<phien ban>\packages`).
4. **Phan phoi file .dynx** - chon 1 trong 2:
   - Tha vao folder share roi tao `C:\ProgramData\BIMLab\DynLock\config.json`:
     ```json
     { "ScriptFolders": [ "\\\\server\\share\\dynx" ] }
     ```
   - Hoac tao folder `Scripts` canh `DynLock.Addin.dll` va bo file `.dynx` vao.
5. **Test**: mo Revit -> tab **BIMLab** -> **Run Tool** -> chon script -> dien
   5 thong so (layer coc, level, quet chon CAD, family coc, ten parameter) ->
   Chạy. Kiem tra coc duoc model dung nhu khi chay bang Dynamo thuong.

## Nhan vien thay gi

Chay **BIMLab Player.exe** -> nhap Auth server URL va Gmail da duoc cap quyen ->
cai add-in cho Revit 2024/2025/2026. Mo Revit -> tab **BIMLab** ban dau co panel
**Manager** voi nut **Login** va **Load**. Dang nhap Gmail trong Revit, bam
**Load** de chon file `.dynx` leader gui, add-in se tao nut plugin tu metadata
trong file `.dynx`. Khi bam plugin, member chi thay form nhap thong so/chon CAD,
khong thay Dynamo graph hay file JSON doc duoc.

## Gioi han can biet

- **Key ma hoa nam san trong app**: nguoi dung Leader/Member khong can cau hinh key.
  Nguoi biet decompile (dnSpy) co the moi key ra. Voi muc
  dich noi bo la du; muon chac hon thi obfuscate DLL (ConfuserEx/Dotfuscator)
  hoac chuyen key sang server cap phat.
- **File tam**: luc chay, graph giai ma duoc ghi ra `%TEMP%` voi ten ngau nhien
  trong vai giay roi xoa ngay. Nguoi dung thuong khong can thiep kip.
- **Node input ho tro tren form**: String, Number, Bool, dropdown Level,
  dropdown Family Type, chon element (1 hoac nhieu). Kieu input khac se giu
  nguyen gia tri da luu trong graph.
- Neu doi key nang cao bang env/secrets.json, moi file `.dynx` cu phai ma hoa lai.

## Cach addin chay graph ngam (ky thuat)

- Dung journal `dynAutomation=true` -> Dynamo chay **dong bo, test mode**, khong UI.
  Trong che do nay Dynamo **bo qua `dynPathExecute`** va khong bao gio tu goi
  `Run()` - graph chi chay neu workspace o **RunType Automatic**. Vi vay addin
  luon ep `RunType=Automatic` + `HasRunWithoutCrash=true` vao JSON truoc khi chay
  (team lead luu graph Manual hay Automatic deu duoc).
- Package van duoc Dynamo nap binh thuong trong automation mode
  (tu `%AppData%\Dynamo\Dynamo Revit\<ver>\packages`). Luu y: may chua tung mo
  Dynamo UI lan nao se chua co thu muc package (test mode khong migrate) -
  mo Dynamo mot lan neu can cai package.
- Sau khi chay, addin doc trang thai tung node (qua reflection) de bao node
  thieu package (DummyNode) va canh bao/loi cua node, roi xoa workspace
  (graph da giai ma) khoi bo nho Dynamo.

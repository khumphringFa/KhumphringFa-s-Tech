# KhumphringFa's Tech

Windows security utilities developed under the KhumphringFa's Tech name.

## USB Guard (prototype)

`src/UsbGuard` contains a Windows desktop proof-of-concept that pairs a removable USB drive, monitors it, and invokes Windows' native lock when the drive disappears. It never bypasses Windows sign-in and **cannot unlock an already locked Windows session**.

### Requirements

- Windows 10/11
- .NET 8 SDK for building
- A removable USB flash drive

### Run from source

```powershell
dotnet run --project src/UsbGuard/UsbGuard.csproj
```

1. Insert a removable USB flash drive.
2. Select it in the app and click **Pair selected USB**. This writes a random key file to the selected drive and stores a protected local verifier.
3. Click **Start monitoring** and test removing the USB. **Save your work first**: Windows will lock.
4. Reinsert the USB and unlock Windows using your normal Windows PIN/password. You can then stop monitoring.

### Security / limitations

- **Prototype only**; not a replacement for Windows Hello, BitLocker, or Windows account authentication.
- Never stores your Windows password and never installs a credential provider.
- Pairing writes only a `.khumphring-usbguard.key` file; it does not format the USB.
- The local verifier uses Windows DPAPI and the USB contains a high-entropy random token. Possession of a copied token can impersonate the paired USB; USB hardware attestation is *not* provided.
- Device removal locking requires the app to be running in the interactive user session. Closing the program stops monitoring.
- The app deliberately offers **no automatic unlock** or login bypass.
- Keep your Windows PIN/password available, and test on a non-critical PC.

Build a release EXE with:

```powershell
dotnet publish src/UsbGuard/UsbGuard.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

Result: `publish/UsbGuard.exe`. Windows security warnings or code-signing prompts may appear for unsigned development builds.

## v0.3 monitoring and Windows Hello

- Optional **3–30 second** missing-key delay (default 3 seconds); reinsert during the delay to cancel locking.
- View the local security event log using **View event log**. It is stored under `%LOCALAPPDATA%\\KhumphringFaTech\\UsbGuard\\events.log`.
- Native Windows locking remains in place. To unlock, use your configured **Windows Hello PIN, fingerprint, face recognition, or Windows password** from the Windows sign-in screen. USB insertion alone cannot unlock Windows.
- Monitoring stops after requesting the native lock; after returning to Windows, click **Start monitoring** to rearm it. This prevents repeated lock requests or an unexpected new lock while signing in.
- The start-with-Windows option only launches the app; it does not automatically arm monitoring.

**Test carefully:** Save work, pair your USB, select a delay, start monitoring, remove the USB, then reinsert within the delay and verify that the lock is canceled. Repeat with the USB absent beyond the delay and verify Windows locks. Reinsert and sign in with Windows Hello, then rearm monitoring.


## v0.5 — USB Raptor-style convenience mode

USB Guard now offers **two different modes**:

- **Default (convenience screen)**: remove the paired USB -> the app displays its full-screen reminder. Reinsert the paired USB -> the app automatically dismisses that screen and continues monitoring. **This does not lock or unlock Windows. It can be bypassed or terminated, and must NOT be treated as protection against an unauthorized user.**
- **Native Windows Lock** (check the option): remove the paired USB -> Windows calls `LockWorkStation()`. Windows Hello PIN, biometric verification, or account credentials are still needed to unlock. Reinsert alone cannot unlock it.

### Test v0.5

1. Save open files and keep Windows PIN/password available.
2. Download the successful GitHub Actions artifact for the latest commit; close any old copy of the app before launching it.
3. Insert an already paired removable USB, leave **Use native Windows Lock** unchecked, and select **Start monitoring**.
4. Remove USB and allow the configured 3–30 second delay. A clearly labeled convenience screen should appear.
5. Reinsert the USB. The convenience screen should disappear without Windows credentials.
6. If needed, click **Stop protection / close screen** to exit convenience mode.
7. Verify **Native Windows Lock** mode separately. It always requires Windows authentication to resume.

**Warning**: The convenience screen is only a visible reminder/deterrent and is not a system security boundary. For real security use native Windows locking and BitLocker where appropriate. The ordinary USB key file is copyable; it is not hardware-backed authentication.

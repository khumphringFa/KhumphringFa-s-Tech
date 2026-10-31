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

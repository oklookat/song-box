# song-box/builder

Builds x64 release version, and copies it to server via SSH. Like update.

## Requiremenets

- Go.
- Visual Studio with .NET, WPF.

## Step 1

1. Open project in VS.
2. Build soulution: Release, x64.

## Step 2

In this script dir.

1. Fill credentials in this script.
2. Create and fill `sing-box.json` with sing-box config.
3. Create and fill `song-box.json`.

`song-box.json` schema:

```json
{
  "appUpdater": {
    "autoUpdate": true,
    "autoUpdateEveryMinutes": 60,
    // update.json will be auto-created when you execute this script
    "updateInfoUrl": "direct link to update.json file",
    "userAgent": "optional"
  },
  "singBox": {
     // tested on this sing-box archive
    "downloadUrl": "https://github.com/SagerNet/sing-box/releases/download/v1.11.13/sing-box-1.11.13-windows-amd64.zip"
  },
  "singBoxConfig": {
    "autoUpdate": true,
    "autoUpdateEveryMinutes": 60,
    "userAgent": "optional",
    "downloadUrl": "direct link to sing-box json config file"
  }
}
```

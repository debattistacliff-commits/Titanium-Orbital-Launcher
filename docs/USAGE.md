# Usage guide

## Orbital ring

The ring displays up to ten items per page. Drag around it to rotate the array. A short synthesized mechanical detent marks each step, and the node facing the hub becomes the selected item.

- Click a selected application, shortcut, file, or folder to open it.
- Use **Previous** and **Next** to move between orbit pages.
- Use **Hide** to collapse the orbital interface when you want an unobstructed background.

## Adding apps and folders

- **Add App** accepts executables and launchable Windows shortcuts/scripts.
- **Add Folder** adds a folder without moving it.
- A search result can also be added to the orbit from its context action.

Favorites are persisted in `%LOCALAPPDATA%\DesktopOrbit\settings.json`.

## Search

The catalog includes accessible entries from:

- Current-user and all-users Start Menu folders
- Current-user and public Desktop folders
- Fixed drives

Recognized entries include folders plus `.lnk`, `.url`, `.exe`, `.bat`, `.cmd`, `.ps1`, `.txt`, `.md`, `.pdf`, `.docx`, `.xlsx`, `.png`, `.jpg`, `.mp4`, and `.mp3` files.

Double-click a search result to open it. System and protected locations are skipped when inaccessible.

## World radio

Select **Radio**, then choose a region:

- **WORLD** requests a broad station list.
- **EUROPE** combines stations from a curated set of European country codes.
- **UK** filters to Great Britain.

Type a station name and select **TUNE**, or double-click a listed station. While audio is playing, the corner monitor displays an animated synthesizer-style level visualization. Select **STOP** to end playback.

Internet radio is live third-party content. A listing does not guarantee that a stream is online, compatible, licensed in every region, or free of advertising.

## Clock

The orbital hub shows local Windows time as `HH:mm:ss`, with values from `00:00:00` through `23:59:59`. The date uses `dd/MM/yyyy`.

## Custom backgrounds

Select **Background**, then choose a JPG, JPEG, PNG, BMP, GIF, or WebP image. The selected path is remembered across launches. Select **Default** to restore the bundled animated circuit-board wallpaper.

If a custom file is moved or deleted, restore the default or select it again from its new location.

## Desktop Organizer

**Organize Desktop** moves eligible desktop entries into:

```text
Documents\Desktop Orbit Library\Shortcuts
Documents\Desktop Orbit Library\Folders
Documents\Desktop Orbit Library\Files
```

Duplicate names receive a numeric suffix rather than being overwritten.

> [!WARNING]
> This command changes file locations. Back up important content, close open documents, and verify sync status if your Desktop is managed by OneDrive or another cloud provider.

The right-side library buttons open each managed location directly.

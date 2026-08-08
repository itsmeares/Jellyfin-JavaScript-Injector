# Jellyfin Plugin - JavaScript Injector

JavaScript Injector lets you manage and inject multiple independent JavaScript snippets into the Jellyfin Web UI from a single plugin configuration page.

This repository is a fork of [n00bcodr/Jellyfin-JavaScript-Injector](https://github.com/n00bcodr/Jellyfin-JavaScript-Injector) with Jellyfin 12 support.

## Features

- Multiple independently managed scripts
- Enable/disable scripts without deleting them
- Optional authenticated-only scripts
- Search, drag-to-reorder, import and export
- Plugin API for other Jellyfin plugins to register scripts
- Jellyfin 12 request-time injection without modifying `jellyfin-web/index.html`

## Jellyfin 12

Jellyfin 12 uses the fork's request-time ASP.NET middleware injector. The loader is added to the Jellyfin Web `index.html` response in memory and the file on disk is left untouched.

This means Jellyfin 12 does **not** require File Transformation, a writable Jellyfin Web directory, or an `index.html` bind mount.

The Jellyfin 12 build targets .NET 10 and Jellyfin `12.0.0-rc1` API packages so it can load on Jellyfin 12 RC and later compatible 12.x servers.

### Install on Jellyfin 12

After the first Jellyfin 12 release has been published, add this repository in **Dashboard → Plugins → Catalog → Settings**:

```text
https://raw.githubusercontent.com/itsmeares/Jellyfin-JavaScript-Injector/main/manifest-v12.json
```

Then install **JavaScript Injector** from the catalog and restart Jellyfin.

The release workflow populates `manifest-v12.json` with the release ZIP URL, MD5 checksum, target ABI and timestamp when a Jellyfin 12 release is published.

## Jellyfin 10.10 / 10.11

The original upstream repositories remain the recommended installation source for Jellyfin 10.x:

**Jellyfin 10.11**

```text
https://raw.githubusercontent.com/n00bcodr/jellyfin-plugins/main/10.11/manifest.json
```

**Jellyfin 10.10.7**

```text
https://raw.githubusercontent.com/n00bcodr/jellyfin-plugins/main/10.10/manifest.json
```

The source tree still contains the 10.10.7 and 10.11 build targets; the Jellyfin 12 release workflow only publishes the 12.x artifact.

## Configuration

After installation, open **Dashboard → Plugins → JavaScript Injector**, or use **JS Injector** in the dashboard sidebar.

1. Click **Add Script**.
2. Give the script a descriptive name.
3. Paste the JavaScript into the editor.
4. Enable the script.
5. Enable **Requires Authentication** when the script should only run after a user is logged in.
6. Save and refresh Jellyfin Web.

Changes are loaded dynamically after a browser refresh; a Jellyfin server restart is not required for normal script edits.

## Jellyfin 12 injection model

On Jellyfin 12 the plugin registers an `IStartupFilter` that intercepts only the Jellyfin Web shell response. It:

- handles `/web`, `/web/` and `/web/index.html`, including base-URL-prefixed deployments;
- requests an uncompressed full HTML response before rewriting it;
- injects the existing JavaScript Injector bootstrap immediately before `</body>`;
- avoids duplicate injection if the bootstrap is already present;
- removes response validators that no longer apply after the in-memory rewrite;
- serves the original response unchanged if injection fails.

The existing `public.js` and authenticated `private.js` loading behavior is preserved.

## Development

### Jellyfin 12 build

```bash
dotnet restore Jellyfin.Plugin.JavaScriptInjector/Jellyfin.Plugin.JavaScriptInjector.csproj -p:JellyfinTarget=jf12
dotnet build Jellyfin.Plugin.JavaScriptInjector/Jellyfin.Plugin.JavaScriptInjector.csproj --configuration Release --no-restore -p:JellyfinTarget=jf12
```

Available build targets:

| Target | Jellyfin | Runtime |
| --- | --- | --- |
| `jf12` | 12.x | .NET 10 |
| `jf11` | 10.11.x | .NET 9 |
| `jf10` | 10.10.7 | .NET 8 |

### Release process

Jellyfin 12 releases are intentionally manual:

1. Merge the release-ready changes to `main`.
2. Run the **Release Jellyfin 12** workflow from GitHub Actions.
3. The workflow reads `AssemblyVersion`, builds the `jf12` target, creates `Jellyfin.Plugin.JavaScriptInjector_12.0.zip`, publishes a GitHub release using that version, calculates the MD5 checksum, and updates `manifest-v12.json` on `main`.

This keeps the catalog manifest checksum tied to the exact published ZIP.

## Plugin interface

Other Jellyfin plugins can register scripts through the existing `IJavaScriptRegistrationService` / `PluginInterface` API. Existing registration payloads and script-management behavior are unchanged by the Jellyfin 12 port.

## Credits

This fork builds on [n00bcodr/Jellyfin-JavaScript-Injector](https://github.com/n00bcodr/Jellyfin-JavaScript-Injector), which itself builds on the original work of [johnpc/jellyfin-plugin-custom-javascript](https://github.com/johnpc/jellyfin-plugin-custom-javascript).

The Jellyfin 12 request-time injection approach follows the same ASP.NET `IStartupFilter` pattern proven by [Jellyfin Enhanced](https://github.com/n00bcodr/Jellyfin-Enhanced).

## Security

Custom JavaScript runs inside the Jellyfin Web application and can access the same browser context as the logged-in user. Only install or write scripts you trust and understand.

## License

GPL-3.0. See [LICENSE](LICENSE).

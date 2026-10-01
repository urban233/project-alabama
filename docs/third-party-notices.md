# Third-party dependencies

The repository's MIT license applies to original Project Alabama code and documentation. It does not replace the terms of Unity, Unity packages, the official project template, or third-party assets.

Artwork and its license evidence are distributed in the separate asset ZIP, not in the public repository. Importing it restores the `source-art/` license records referenced here and in `docs/assets.json`. The adapted E46 is credited to BlenderCentral under CC-BY-SA 3.0; the industrial kit and Poly Haven resources use CC0. See the restored per-asset records for attribution, modifications, and source URLs.

| Component | Source | License record |
| --- | --- | --- |
| Unity 6000.3.25f1 editor/runtime | Unity official release catalog | [Unity software terms](https://unity.com/legal/editor-terms-of-service/software); installation excluded from Git |
| Universal 3D project template | `com.unity.template.urp-blank`, Unity catalog | Unity template/package terms; retained serialized template settings are identified as Unity-provided configuration |
| URP and dependencies | Exact versions in `unity/Packages/packages-lock.json` | Preserve each resolved package's LICENSE/Third Party Notices; restore through Package Manager |
| Input System | `com.unity.inputsystem`, pinned manifest version | Resolved package LICENSE/Third Party Notices |
| Unity Test Framework and NUnit | Package Manager | Resolved package LICENSE/Third Party Notices |
| Blender | Locally installed Blender 4.4.0 | Tool license belongs to Blender; the installation is outside the repository |
| Calibration mesh/source | Authored by this project's generator | CC0; see `source-art/LICENSE.md` and `docs/assets.json` |

No Unity package source code or editor installation is vendored or relicensed as project-authored code. The provided screenshot is ignored local reference material; its redistribution rights have not been established.

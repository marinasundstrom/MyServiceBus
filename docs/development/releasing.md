# Publishing a Preview Release

The NuGet preview is published manually by the `Publish NuGet preview` GitHub Actions workflow. The workflow builds, tests, packages, and verifies the checked-out commit before requesting a short-lived NuGet.org API key through GitHub OIDC. It does not use a long-lived NuGet API key.

## One-time NuGet.org setup

Create a trusted publishing policy for the NuGet.org account that owns the MyServiceBus package IDs. Use these values after the workflow file has been merged or pushed to GitHub:

| Policy field | Value |
| --- | --- |
| Policy name | `MyServiceBus Distribution` |
| Package owner | `marna.li` |
| Repository owner | `marinasundstrom` |
| Repository | `MyServiceBus` |
| Workflow file | `publish-nuget.yml` |
| Environment | Leave empty |

Enter only the workflow filename, not the `.github/workflows/` path.

The workflow supplies the public NuGet.org profile username `marna.li` directly to `NuGet/login`. No NuGet credential, API key, or repository secret is required. GitHub OIDC is exchanged for a short-lived API key immediately before publication.

The trusted policy owner must own all four package IDs:

- `Sundstrom.MyServiceBus.Abstractions`
- `Sundstrom.MyServiceBus`
- `Sundstrom.MyServiceBus.RabbitMq`
- `Sundstrom.MyServiceBus.Testing`

## Publishing

1. Update `VersionPrefix` and `VersionSuffix` in `Directory.Build.props`, and update the matching Java version and version-specific documentation.
2. Run the complete release-candidate gate on the intended commit and ensure all required GitHub Actions checks pass for that commit.
3. In GitHub Actions, select **Publish NuGet preview**, choose the intended branch or tag, and run the workflow.
4. Confirm the selected commit and package version in the workflow log.
5. Download the uploaded workflow artifact if an archival copy is needed, and verify all four packages and symbol packages on NuGet.org after indexing completes.

The workflow accepts no version input. It reads the package version from the selected commit and refuses to publish a version that does not contain the `-preview.` prerelease suffix. NuGet.org package versions are immutable. `--skip-duplicate` allows a failed multi-package publication to be resumed without replacing packages that were already accepted.

The Maven Central publication should use the same source commit and semantic version. Its credentials and publishing tasks remain separate from NuGet trusted publishing.

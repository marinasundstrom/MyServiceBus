# Publishing a Preview Release

MyServiceBus publishes matching .NET and Java previews from one release commit. Both publication workflows are manually dispatched and derive the version from the selected Git ref; neither accepts a version override.

| Ecosystem | Workflow | Registry |
| --- | --- | --- |
| .NET | `Publish NuGet preview` (`publish-nuget.yml`) | NuGet.org |
| Java | `Publish Maven Central preview` (`publish-maven.yml`) | Maven Central Publisher Portal |

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

Enter only the workflow filename, not the `.github/workflows/` path. The workflow supplies the public NuGet.org profile username directly to `NuGet/login`; no NuGet API-key secret is required. GitHub OIDC is exchanged for a short-lived key immediately before publication.

The trusted policy owner must own all four package IDs:

- `Sundstrom.MyServiceBus.Abstractions`
- `Sundstrom.MyServiceBus`
- `Sundstrom.MyServiceBus.RabbitMq`
- `Sundstrom.MyServiceBus.Testing`

## One-time Maven Central setup

Verify that the Portal account has the GitHub-backed `io.github.marinasundstrom` namespace; the project publishes beneath it as `io.github.marinasundstrom.myservicebus`. Create a current Portal user token for that account. The workflow uses these repository secrets:

| Secret | Content |
| --- | --- |
| `OSSRH_USERNAME` | Central Portal token username |
| `OSSRH_TOKEN` | Central Portal token password |
| `MAVEN_SIGNING_KEY` | ASCII-armored PGP private key |
| `MAVEN_SIGNING_PASSWORD` | Private-key passphrase |

The `OSSRH_` names are retained for compatibility with the existing repository configuration, but their values must be a Central Portal user token. Legacy OSSRH credentials do not work with the Portal API.

Maven Central requires the binary JAR, source JAR, Javadoc JAR, POM, Gradle module metadata, and detached PGP signatures. Publish the signing key's public half to a Central-supported keyserver so Central and consumers can verify the signatures. Keep the private key and passphrase only in GitHub secrets.

## Preparing a release candidate

1. Update `VersionPrefix` and `VersionSuffix` in `Directory.Build.props`.
2. Set the identical version in the root `build.gradle` file.
3. Update version-pinned package smoke tests, README installation commands, and version-specific documentation.
4. Run `./eng/verify-release-versions.sh <version>` to prove the .NET and Java build versions match.
5. Run the complete .NET, Java, RabbitMQ, package-consumer, and interoperability gates.
6. Commit the release candidate and create a tag such as `v0.1.0-preview.2` on that exact commit.
7. Wait for all required GitHub Actions checks on the tag's commit to pass.

Using one immutable tag for both workflows prevents a branch update from causing the registries to receive artifacts built from different commits.

## Publishing both ecosystems

1. In GitHub Actions, run **Publish Maven Central preview** and select the release tag.
2. Wait for the workflow to reach Maven Central state `PUBLISHED`. It tests all Java modules, creates signed publications, verifies a clean consumer, uploads one bundle containing all seven artifacts, and waits for automatic validation and release.
3. Run **Publish NuGet preview** and select the same release tag.
4. Confirm that all four NuGet packages and symbol packages were accepted.
5. Verify the version on Maven Central and NuGet.org after registry indexing completes.
6. Create the GitHub prerelease and use the same version in its title and release notes.

Maven Central is published first because its validation is stricter and can reject an entire deployment before release. Once both workflows succeed, the release tag, Maven coordinates, NuGet package versions, and GitHub prerelease all identify the same source state.

The `0.1.0-preview.1` NuGet packages were built from commit `e0314869a00daed55613dfe8c7568190c7793eee`, before the Maven workflow existed. For this inaugural Java publication, use the first release-publication commit and verify that no Java or .NET product source changed after `e031486`; future releases must use one shared tag from the outset.

## Failure and retry behavior

NuGet and Maven Central package versions are immutable. The NuGet workflow uses `--skip-duplicate`, allowing an interrupted multi-package push to resume without replacing accepted packages. Maven Central uploads all seven Java artifacts as one deployment bundle; validation failure leaves the deployment failed rather than partially publishing individual modules.

Do not rebuild an already-published version from another commit. If a released artifact is defective, fix it, increment the preview version, repeat the complete candidate gate, and publish a new release tag.

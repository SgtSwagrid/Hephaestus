# 📜 Contribution Guidelines

This document lays out the basic principles which govern contributions to this project.

## 🛠️ Workflow

1. Clone the repository: `git clone https://github.com/SgtSwagrid/<repository-name>.git`.
2. Create a feature branch: `git switch -c feature_<description>`.
3. Make your changes (use of [Rider](https://www.jetbrains.com/rider/) or [Visual Studio Code](https://code.visualstudio.com/) is recommended but not required).
4. Reformat the code according to our style rules: `dotnet format`.
5. Commit (`git add -A` then `git commit -m "<description>"`).
6. Push your changes, either to this repository directly if you have write access, or to a forked version of it if you don't.
7. Create a pull request to `main`, with the title given as `[<scope>] <Description>` (e.g. `[rendering] Solved inverted colours.`).
8. Before merging, the code must pass the build test (defined in [build-integrity.yml](./.github/workflows/build-integrity.yml)).

## 🎩 Code Style

Please see the [Code Style Guidelines](docs/STYLE_GUIDE.md) for details on our design principles and code style.
In brief:
- We adhere to the principles of [functional programming](https://en.wikipedia.org/wiki/Functional_programming).
- [`dotnet format`](https://learn.microsoft.com/dotnet/core/tools/dotnet-format) will automatically reformat your code for you, according to [.editorconfig](.editorconfig).

## 🌳 Branching Strategy

This project follows simple [trunk-based](https://trunkbaseddevelopment.com/) development.
All work happens on short-lived feature branches, which are merged into `main` by pull request.
Releases are by [tag](https://docs.github.com/en/repositories/releasing-projects-on-github/viewing-your-repositorys-releases-and-tags),
and there are no separate release branches.

## 📢 Publishing Workflow

GitHub releases are automatically published to [NuGet](https://www.nuget.org/) upon creation, with the version taken from the release tag.

### Example

To release version `1.2.3`, go to **Releases → Draft a new release**, create the tag `v1.2.3`, and click **Publish release**.
Note the inclusion of `v` in the GitHub release name but not the resulting NuGet version.
A tag with a suffix, such as `v1.2.3-beta.1`, publishes a [prerelease](https://learn.microsoft.com/nuget/create-packages/prerelease-packages),
which NuGet hides from users unless they opt in.

## 🤖 LLM Agent Usage

The use of agents where it makes sense is allowed and encouraged.
In particular, this project is configured for optimal interoperability with [Claude Code](https://claude.com/product/claude-code).
You are however free to use whichever tooling you wish.

### Responsibility

You are still responsible for code that was written or partially written by an LLM.
You should always read and understand all changes before submitting them.

Generally, the reviewer doesn't need to know which code was generated or manually created.
Such a distinction would undermine any sense of responsibility: all of the code belongs to you.
An exception exists when an LLM was used to perform a menial transformation en masse,
in which case you might consider isolating such changes to a stand-alone commit, and sharing the prompt.

## 📞 Contact

Created by [Alec Dorrington](https://github.com/SgtSwagrid).
For questions or issues, please use the GitHub issue tracker.

## 🔁 Origin

The primary source of truth for this document can be found in the [C# Library Config](https://github.com/SgtSwagrid/cs-library-config) repository,
from which it is automatically synchronised with [GitHub Graph](https://github.com/SgtSwagrid/github-graph).
This should be updated there rather than here, lest any changes be subsequently reverted.

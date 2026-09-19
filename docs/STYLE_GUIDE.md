# 📜 Code Style Guidelines

This document lays out the basic principles which govern the coding style in this project.
The rules here are not of a strictly binding nature, and can be broken when there is good reason for doing so.
While this document is for humans too, some of the excessive specificity is for the sake of LLM agents.

## 🤖 dotnet format

The guidelines here are supplemented by [`dotnet format`](https://learn.microsoft.com/dotnet/core/tools/dotnet-format),
which provides automatic reformatting based on the style configuration in [.editorconfig](../.editorconfig).
You can reformat your local changes using `dotnet format`, or by enabling "reformat on save" in your IDE (recommended).
You should ensure that your code is correctly formatted before submitting it,
or else the CI pipeline won't allow it to be merged.

## 🎩 Code Style

### Structure and design

- This project follows a [functional](https://en.wikipedia.org/wiki/Functional_programming) style:
  - Use pure functions without [side effects](https://en.wikipedia.org/wiki/Side_effect_(computer_science)), or with _explicit_ side effects where unavoidable.
  - Use [immutable](https://en.wikipedia.org/wiki/Persistent_data_structure) data structures and variables, where state updates are by duplication (e.g. `with` expressions) rather than by mutation.
  - Consistently use fine-grained [types](https://shekhar14.medium.com/type-theory-and-functional-programming-52f81deb36f1).
  - Avoid local or multiple returns and variable reassignment, and instead use expression-based conditionals (`? :`), `switch` expressions and pattern matching.
- When performance constraints or imperative APIs necessitate the use of mutable state in a limited scope, this is allowed.
- Hide complexity behind well-named mathematical abstractions where possible, so that the code reads like the math it represents.
- Use higher-order functions and LINQ combinators (e.g. `Select`, `SelectMany`, `Aggregate`, etc.)
  to abstract over common patterns of computation, rather than writing explicit loops.
- Actively check for and avoid code duplication, and try to unify implementations where possible.
  - Removing duplicated code will quite possibly involve rewriting existing functions with a more general interface.
- Write very short methods (ideally 1-3 lines) that do one thing, and form more complex behaviour by composing them.
  If a method is longer than 5 lines, consider breaking it up.

### Types

- Prefer `record`s and `interface`s to classes.
  - Model an algebraic data type as an `interface` with a `sealed record` for each case.
  - Use `static class`es only as containers for functions and extension members.
- Keep data separate from behaviour.
  Records hold plain data exactly as written, while operations on them (including operators)
  are C# 14 [extension members](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-14#extension-members) in a separate `static class`.
- Don't normalise, flatten or simplify data at construction time; do so in a later, explicit pass.
- Make illegal states unrepresentable, so that mistakes are caught by the compiler rather than at runtime.

### Syntax

- Use the latest version of C# and its features, and in particular:
  - `extension` blocks rather than `this`-parameter extension methods.
  - File-scoped namespaces.
  - Collection expressions (`[a, b, c]`) and primary constructors.
- Place braces on the same line as the declaration or statement they belong to ([K&R](https://en.wikipedia.org/wiki/Indentation_style#K&R)).
- Use expression-bodied members (`=>`) wherever a member is a single expression.
- Write positional records with one parameter per line:
  ```csharp
  public record Interval(
      double Lower,
      double Upper
  );
  ```
- Use `var` for local variables.
- Place `using` directives at the top of the file, outside the namespace.
- Avoid qualified names unless you have good reason to use them. Instead of `System.Text.StringBuilder`, add `using System.Text;` and just write `StringBuilder`.
- When in doubt, follow the existing style of the codebase.

### Naming conventions

- Follow the standard [.NET naming conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/identifier-names):
  - Use `PascalCase` for types, namespaces, methods, properties, and constants.
  - Use `camelCase` for parameters and local variables.
  - Prefix interface names with `I`.
- Use short yet descriptive names.
  - For example, `Select<Y>(Func<X, Y> transform)` is better than `Select<Y>(Func<X, Y> f)`, but `x` is better than `x1` and `start` is better than `startIndex`.
- Avoid names which describe implementation rather than meaning, such as `VariableReference` or `ExpressionWrapper`.
- Use [Australian English](https://www.macquariedictionary.com.au/) spelling in names and comments (e.g. `Normalise`, `Colour`),
  except where an external API forces otherwise.

### Comments

- Be as concise as possible, avoiding repetition and unnecessary words, while still writing full sentences with clear meaning.
- Add [XML documentation comments](https://learn.microsoft.com/dotnet/csharp/language-reference/xmldoc/) to all public members that are not self-explanatory.
- Prefer writing references to types and members with `<see cref="MyType"/>` rather than `<c>MyType</c>`, so as to establish a navigable link.
- All literals in comments should be wrapped in `<c>`, e.g. `<c>true</c>`, `<c>0</c>`, etc.
  Use `<see langword="null"/>` for keywords.
- All sentences should start with a capital letter and end with a period (`.`).
  This includes within tags such as `<param>` and `<returns>`.
- Parameter descriptions (i.e. with `<param>`) use definite articles (e.g. "The").
- Return value descriptions (i.e. with `<returns>`) use indefinite articles (e.g. "A", "An").

## 🔁 Origin

The primary source of truth for this document can be found in the [C# Library Config](https://github.com/SgtSwagrid/cs-library-config) repository,
from which it is automatically synchronised with [GitHub Graph](https://github.com/SgtSwagrid/github-graph).
This should be updated there rather than here, lest any changes be subsequently reverted.

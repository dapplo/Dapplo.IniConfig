# Validation (INotifyDataErrorInfo)

Dapplo.Ini provides two complementary ways to add validation to a section:

1. **DataAnnotations attributes** — place standard .NET attributes directly on the property and the source generator creates the validation code for you.
2. **`IDataValidation<TSelf>`** — implement a static method inside the interface for custom, per-property rules.

Both approaches surface errors through
`System.ComponentModel.INotifyDataErrorInfo`, so WPF/Avalonia/WinForms bindings
pick up errors automatically.  **No exceptions are ever thrown** — bad values
are loaded normally; errors are stored and available to the consumer.

---

## DataAnnotations attributes (generated rules)

Place standard `System.ComponentModel.DataAnnotations` attributes on any
property of an `[IniSection]` interface.  The source generator automatically:

- implements `INotifyDataErrorInfo` on the generated class;
- runs all attribute-based checks after the file is loaded (`IAfterLoad`); and
- re-runs the check for a property whenever its setter is called.

### Supported attributes

| Attribute | Trigger condition | Default error message |
|---|---|---|
| `[Required]` | `string.IsNullOrEmpty` (strings) / `== null` (nullable refs & nullable value types) | `"{PropertyName} is required."` |
| `[Range(min, max)]` | value outside `[min, max]` (uses `IComparable`) | `"{PropertyName} must be between {min} and {max}."` |
| `[MaxLength(n)]` | `string.Length > n` (null is skipped) | `"{PropertyName} must not exceed {n} characters."` |
| `[RegularExpression(pattern)]` | `Regex.IsMatch` returns `false` (null is skipped) | `"{PropertyName} does not match the required pattern."` |

All attributes support the `ErrorMessage` property to override the default message.

```csharp
[IniSection("Server")]
public interface IServerSettings : IIniSection
{
    // Required — null/empty triggers an error
    [Required(ErrorMessage = "Host is required.")]
    string? Host { get; set; }

    // Range — value outside 1–65535 triggers an error
    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535.")]
    [IniValue(DefaultValue = "8080")]
    int Port { get; set; }

    // MaxLength — more than 50 characters triggers an error
    [MaxLength(50)]
    string? Tag { get; set; }

    // RegularExpression — must be alphanumeric; null is always skipped
    [RegularExpression(@"^[a-zA-Z0-9]+$", ErrorMessage = "Code must be alphanumeric.")]
    string? Code { get; set; }
}
```

The generated class derives from `INotifyDataErrorInfo`, so WPF/Avalonia
bindings are satisfied without any additional code.

---

## `IDataValidation<TSelf>` — custom rules (C# 11+ / .NET 7+, recommended)

Use this pattern when your validation logic requires comparing several
properties against each other or calling external services.

```csharp
[IniSection("Server")]
public interface IServerSettings : IIniSection, IDataValidation<IServerSettings>
{
    [IniValue(DefaultValue = "8080", NotifyPropertyChanged = true)]
    int Port { get; set; }

    [IniValue(DefaultValue = "localhost", NotifyPropertyChanged = true)]
    string? Host { get; set; }

    static new IEnumerable<string> ValidateProperty(IServerSettings self, string propertyName)
    {
        return propertyName switch
        {
            nameof(Port) when self.Port is < 1 or > 65535
                => new[] { "Port must be between 1 and 65535." },
            nameof(Host) when string.IsNullOrWhiteSpace(self.Host)
                => new[] { "Host must not be empty." },
            _ => Array.Empty<string>()
        };
    }
}
```

> Validation re-runs whenever a property annotated with
> `NotifyPropertyChanged = true` (or carrying a DataAnnotations attribute)
> changes its value.  For all other properties, call the framework's
> `RunValidation(nameof(MyProp))` helper from a partial-class override to
> trigger validation explicitly.

---

## Combining DataAnnotations and custom rules

Both rule sets are **merged**: generated attribute rules are checked first,
then custom `IDataValidation<TSelf>` rules are appended.

```csharp
[IniSection("Server")]
public interface IServerSettings : IIniSection, IDataValidation<IServerSettings>
{
    [Required(ErrorMessage = "Host is required.")]
    [IniValue(NotifyPropertyChanged = true)]
    string? Host { get; set; }

    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535.")]
    [IniValue(DefaultValue = "8080", NotifyPropertyChanged = true)]
    int Port { get; set; }

    // Custom rule — applied in addition to the generated [Required] check
    static new IEnumerable<string> ValidateProperty(IServerSettings self, string propertyName)
    {
        if (propertyName == nameof(Host) &&
            string.Equals(self.Host, "banned", StringComparison.OrdinalIgnoreCase))
            yield return "The hostname 'banned' is not allowed.";
    }
}
```

---

## Legacy instance-method pattern (.NET Framework / non-generic)

For .NET Framework 4.x or when you prefer instance methods in a separate file,
implement the non-generic `IDataValidation` and provide the implementation in a partial class:

```csharp
[IniSection("Server")]
public interface IServerSettings : IIniSection, IDataValidation
{
    [IniValue(DefaultValue = "8080", NotifyPropertyChanged = true)]
    int Port { get; set; }
}

// Partial class provides the instance implementation
public partial class ServerSettingsImpl
{
    public IEnumerable<string> ValidateProperty(string propertyName)
    {
        if (propertyName == nameof(Port) && Port is < 1 or > 65535)
            yield return "Port must be between 1 and 65535.";
    }
}
```

---

## Handling validation results

The generated class implements `INotifyDataErrorInfo`.  There are several ways
to consume the errors depending on your context.

### WPF / Avalonia binding

Bindings automatically call `GetErrors` when `ErrorsChanged` fires.
Add `Validation.ErrorTemplate` to your control to display them:

```xml
<!-- WPF — ValidatesOnNotifyDataErrors is true by default -->
<TextBox Text="{Binding Port, UpdateSourceTrigger=PropertyChanged}" />
```

### Code-behind / service layer

Inspect errors directly through the `INotifyDataErrorInfo` interface:

```csharp
var section = new ServerSettingsImpl();
// … build/load …

var errorInfo = (INotifyDataErrorInfo)section;

// Check whether any property is in error
if (errorInfo.HasErrors)
{
    // Retrieve errors for one property
    var portErrors = errorInfo.GetErrors(nameof(IServerSettings.Port))
                              .Cast<string>()
                              .ToList();

    // Retrieve all errors (pass null or empty string)
    var allErrors = errorInfo.GetErrors(null)
                             .Cast<string>()
                             .ToList();
}

// Subscribe to changes
errorInfo.ErrorsChanged += (_, e) =>
    Console.WriteLine($"Validation changed for: {e.PropertyName}");
```

### IDataValidation dispatch interface

For headless/service scenarios you can call `ValidateProperty` directly via
the non-generic `IDataValidation` dispatch interface:

```csharp
var validation = (IDataValidation)section;
var errors = validation.ValidateProperty(nameof(IServerSettings.Port)).ToList();
```

This works whether the section uses DataAnnotations attributes, `IDataValidation<TSelf>`,
or both.

### Post-load validation in a lifecycle hook

If the section does **not** use `NotifyPropertyChanged = true` but you still
want to surface all validation errors after the file is loaded, use the
generated `RunAllAttributeValidations()` helper from an `IAfterLoad` hook in
a partial class:

```csharp
// In a separate partial class file:
public partial class ServerSettingsImpl : IAfterLoad
{
    public void OnAfterLoad()
    {
        RunAllAttributeValidations(); // re-validates all DataAnnotations-annotated properties
    }
}
```

> **Note:** When a section uses only DataAnnotations attributes (no
> `IDataValidation<TSelf>`), the generator emits an `IAfterLoad` bridge
> automatically, so validation runs immediately after the file is loaded even
> without an explicit lifecycle hook.

---

## WPF / Avalonia binding

The generated class automatically implements `INotifyDataErrorInfo`, so WPF/Avalonia
bindings pick up errors without any additional code:

```xml
<!-- WPF XAML — Binding.ValidatesOnNotifyDataErrors=True is the default in .NET -->
<TextBox Text="{Binding Port, UpdateSourceTrigger=PropertyChanged}" />
```

---

## See also

- [[Property-Change-Notifications]] — `INotifyPropertyChanged` / `INotifyPropertyChanging`
- [[Defining-Sections]] — standard .NET attribute support (`[DefaultValue]`, `[Description]`, `[DataMember]`, `[IgnoreDataMember]`)
- [[Lifecycle-Hooks]] — `IAfterLoad`, `IBeforeSave`, `IAfterSave`

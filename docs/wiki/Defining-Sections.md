# Defining Sections

Every configuration section is a plain C# interface annotated with `[IniSection]`.
The source generator (`Dapplo.Ini.Generator`) creates a concrete `partial class`
implementation automatically.

---

## `[IniSection]` attribute

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SectionName` (ctor) | `string?` | interface name minus leading `I` | Name of the `[Section]` in the INI file |
| `Description` | `string?` | `null` | Written as a comment above the section header |

```csharp
// Section name derived from interface name → "UserProfile"
[IniSection]
public interface IUserProfile : IIniSection { /* … */ }

// Explicit section name
[IniSection("user")]
public interface IUserProfile : IIniSection { /* … */ }
```

---

## `[IniValue]` attribute

Annotate each property with `[IniValue]` to control its INI representation.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `KeyName` | `string?` | property name | Key name in the INI file |
| `DefaultValue` | `string?` | `null` | Raw string parsed via the type's converter |
| `Description` | `string?` | `null` | Written as a comment above the key |
| `ReadOnly` | `bool` | `false` | When `true`, the value is never written to disk |
| `Transactional` | `bool` | `false` | When `true`, the property participates in transactions |
| `NotifyPropertyChanged` | `bool` | `false` | Raises `INotifyPropertyChanged` / `INotifyPropertyChanging` |

```csharp
[IniSection("Database")]
public interface IDbSettings : IIniSection
{
    [IniValue(DefaultValue = "localhost", Description = "Database host", KeyName = "host")]
    string? Host { get; set; }

    [IniValue(DefaultValue = "5432")]
    int Port { get; set; }

    [IniValue(DefaultValue = "True", NotifyPropertyChanged = true)]
    bool EnableSsl { get; set; }
}
```

---

## Read-only properties

A property can be made **read-only from the consumer's perspective** simply by omitting
the setter from the interface declaration (`{ get; }` instead of `{ get; set; }`).

The source generator automatically detects getter-only properties and treats them as
read-only:

| Behaviour | Getter-only `{ get; }` | `[IniValue(ReadOnly = true)]` |
|-----------|----------------------|-------------------------------|
| Default value applied | ✓ | ✓ |
| Value loaded from INI | ✓ | ✓ |
| Value written to INI on save | **✗** | **✗** |
| Setter on **implementation class** | ✓ (public) | ✓ (public) |
| Setter on **interface** | **✗** | ✓ |

### Getter-only interface property

Declare the property without a setter in the interface. The generated implementation
class still exposes a **public setter** so the framework (and code that references the
concrete class directly) can assign the value programmatically.

```csharp
[IniSection("AppInfo")]
public interface IAppInfo : IIniSection
{
    // Getter-only: cannot be set through the interface.
    // The value is loaded from the INI file but never written back.
    [IniValue(DefaultValue = "1.0.0")]
    string? Version { get; }

    // Regular read-write property — written to disk when saved.
    [IniValue(DefaultValue = "MyApp")]
    string? Name { get; set; }
}
```

Usage:

```csharp
IAppInfo settings = new AppInfoImpl();   // concrete type from source generator

// ✓ Reading always works through the interface:
Console.WriteLine(settings.Version);

// ✗ Compile error — interface does not expose a setter:
// settings.Version = "2.0.0";

// ✓ Setting is still possible via the concrete class:
var impl = (AppInfoImpl)settings;
impl.Version = "2.0.0";
```

### `[IniValue(ReadOnly = true)]` on a read-write property

The `ReadOnly` attribute flag achieves the same no-save behaviour while **keeping the
setter on the interface**. This is useful when you need to set the property through the
interface type but still want to prevent it from being written to disk.

```csharp
[IniSection("AppInfo")]
public interface IAppInfo : IIniSection
{
    // Interface setter is present, but the value is never written to disk.
    [IniValue(DefaultValue = "1.0.0", ReadOnly = true)]
    string? Version { get; set; }
}
```

---

## Generated class naming convention

The generator derives the concrete class name from the interface name:

| Interface name | Generated class name | Generated file |
|---------------|---------------------|----------------|
| `IAppSettings` | `AppSettingsImpl` | `AppSettingsImpl.g.cs` |
| `IDbConfig` | `DbConfigImpl` | `DbConfigImpl.g.cs` |
| `IUserProfile` | `UserProfileImpl` | `UserProfileImpl.g.cs` |
| `ServerConfig` *(no leading I)* | `ServerConfigImpl` | `ServerConfigImpl.g.cs` |

The rule is: strip a leading `I` (if present) and append `Impl`.
The file is generated into your project's intermediate output folder and compiled automatically.

Because the generated class is declared `partial`, you can extend it with your own
code in a separate file — see [[Lifecycle-Hooks#legacy-partial-class-pattern]].

---

## Standard .NET attribute support

The source generator also honours several standard .NET attributes as
convenient alternatives to `[IniSection]` and `[IniValue]`.  **`[IniValue]`
always takes precedence** — standard attributes are used only as fallbacks
when the `[IniValue]` equivalent is not specified.

### Metadata attributes (section & property)

| Standard attribute | Equivalent `[IniValue]` / `[IniSection]` field | Notes |
|---|---|---|
| `[Description("...")]` on interface | `[IniSection(Description = "...")]` | Sets the section comment |
| `[Description("...")]` on property | `[IniValue(Description = "...")]` | Sets the key comment |
| `[DefaultValue(value)]` on property | `[IniValue(DefaultValue = "...")]` | Accepts any value type; converted to string internally |
| `[DataMember(Name = "...")]` on property | `[IniValue(KeyName = "...")]` | Sets the INI key name |

### Exclusion attribute

| Standard attribute | Effect |
|---|---|
| `[IgnoreDataMember]` on property | Property is excluded from all INI read/write operations. The backing field and the property itself are still generated so the interface contract is satisfied. |

```csharp
[IniSection("UserProfile")]
[Description("User profile settings")]        // sets section comment
public interface IUserProfileSettings : IIniSection
{
    [DataMember(Name = "display_name")]        // INI key is "display_name"
    [DefaultValue("Anonymous")]                // default value
    [Description("The user's display name")]   // written as a comment
    string? DisplayName { get; set; }

    [DefaultValue(3)]                          // numeric default
    int LoginAttempts { get; set; }

    [IgnoreDataMember]                         // never written to or read from the file
    string? SessionToken { get; set; }
}
```

### Validation attributes (DataAnnotations)

Place `System.ComponentModel.DataAnnotations` attributes on properties to
have the source generator emit inline validation code.  See [[Validation]]
for the full attribute reference and examples of how to consume the
`INotifyDataErrorInfo` errors.

| Attribute | What is checked |
|---|---|
| `[Required]` | null / empty string |
| `[Range(min, max)]` | numeric (or `IComparable`) range |
| `[MaxLength(n)]` | string length |
| `[RegularExpression(pattern)]` | regex match |

All attributes support an `ErrorMessage` property to override the default message.

```csharp
[IniSection("App")]
public interface IAppSettings : IIniSection
{
    [Required]
    string? Name { get; set; }

    [Range(1024, 65535, ErrorMessage = "Port must be between 1024 and 65535.")]
    int Port { get; set; }

    [MaxLength(100)]
    string? Description { get; set; }

    [RegularExpression(@"^[a-z0-9_-]+$", ErrorMessage = "Slug must be lowercase alphanumeric.")]
    string? Slug { get; set; }
}
```

---

## See also

- [[Lifecycle-Hooks]] — `IAfterLoad`, `IBeforeSave`, `IAfterSave`
- [[Validation]] — `IDataValidation<TSelf>`, DataAnnotations attributes, and `INotifyDataErrorInfo`
- [[Transactional-Updates]] — `ITransactional` for atomic updates
- [[Value-Converters]] — supported property types and custom converters

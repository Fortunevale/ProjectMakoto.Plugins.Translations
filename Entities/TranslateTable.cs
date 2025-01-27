using ProjectMakoto.Database;
using ProjectMakoto.Enums;

namespace ProjectMakoto.Plugins.Translations.Entities;

[TableName("users")]
public class TranslateTable : PluginDatabaseTable
{
    public TranslateTable(BasePlugin plugin, ulong identifierValue) : base(plugin, identifierValue)
    {
        this.Id = identifierValue;
    }

    [ColumnName("userid"), ColumnType(ColumnTypes.BigInt), Primary]
    internal ulong Id { get; init; }

    [ColumnName("last_google_source"), ColumnType(ColumnTypes.Text), Nullable]
    public string LastGoogleSource
    {
        get => this.GetValue<string>(this.Id, "last_google_source");
        set => _ = this.SetValue(this.Id, "last_google_source", value);
    }

    [ColumnName("last_google_target"), ColumnType(ColumnTypes.Text), Nullable]
    public string LastGoogleTarget
    {
        get => this.GetValue<string>(this.Id, "last_google_target");
        set => _ = this.SetValue(this.Id, "last_google_target", value);
    }

    [ColumnName("last_libretranslate_source"), ColumnType(ColumnTypes.Text), Nullable]
    public string LastLibreTranslateSource
    {
        get => this.GetValue<string>(this.Id, "last_libretranslate_source");
        set => _ = this.SetValue(this.Id, "last_libretranslate_source", value);
    }

    [ColumnName("last_libretranslate_target"), ColumnType(ColumnTypes.Text), Nullable]
    public string LastLibreTranslateTarget
    {
        get => this.GetValue<string>(this.Id, "last_libretranslate_target");
        set => _ = this.SetValue(this.Id, "last_libretranslate_target", value);
    }
}

// Project Makoto Example Plugin
// Copyright (C) 2023 Fortunevale
// This code is licensed under MIT license (see 'LICENSE'-file for details)

using DisCatSharp.Enums;
using ProjectMakoto.Plugins.Translations.Entities;

namespace ProjectMakoto.Plugins.Translation;

public class TranslationPlugin : BasePlugin
{
    public override string Name => "Translation Commands";
    public override string Description => "Allows users to automatically translate messages using LibreTranslate or Google!";
    public override SemVer Version => new(1, 0, 0);
    public override int[] SupportedPluginApis => [1];
    public override string Author => "Mira";
    public override ulong? AuthorId => 411950662662881290;
    public override string UpdateUrl => "https://github.com/Fortunevale/ProjectMakoto.Plugins.Translations";
    public override Octokit.Credentials? UpdateUrlCredentials => base.UpdateUrlCredentials;

    public static TranslationPlugin? Plugin { get; set; }
    public GoogleTranslateClient? TranslationClient { get; internal set; }

    public SelfFillingDatabaseDictionary<TranslateTable>? UserData { get; set; }

    internal PluginConfig LoadedConfig
    {
        get
        {
            if (!this.CheckIfConfigExists())
            {
                this._logger.LogDebug("Creating Plugin Config..");
                this.WriteConfig(new PluginConfig());
            }

            var v = this.GetConfig(); 

            if (v.GetType() == typeof(JObject))
            {
                this.WriteConfig(((JObject)v).ToObject<PluginConfig>() ?? new PluginConfig());
                v = this.GetConfig();
            }

            return (PluginConfig)v;
        }
    }

    public override TranslationPlugin Initialize()
    {
        TranslationPlugin.Plugin = this;

        this.DatabaseInitialized += (s, e) =>
        {
            this.TranslationClient = new GoogleTranslateClient(this);

            this.UserData = new SelfFillingDatabaseDictionary<TranslateTable>(this, typeof(TranslateTable), (id) =>
            {
                return new TranslateTable(this, id);
            });
        };     

        return this;
    }

    public override Task<IEnumerable<MakotoModule>> RegisterCommands()
    {
        return Task.FromResult<IEnumerable<MakotoModule>>(new List<MakotoModule>
        {
            new("Translation",
                new List<MakotoCommand>()
                {
                    new(ApplicationCommandType.Message, "Translate Message", "Allows you to translate a message. Reply to a message to select it.", typeof(TranslateCommand), "translate")
                })
        });
    }

    public override Task<IEnumerable<Type>?> RegisterTables()
    {
        return Task.FromResult<IEnumerable<Type>?>(new List<Type>
        {
            typeof(TranslateTable),
        });
    }

    public override (string? path, Type? type) LoadTranslations()
    {
        return ("Translations/strings.json", typeof(Entities.Translations));
    }                                                                       

    public override Task Shutdown()
        => base.Shutdown();
}

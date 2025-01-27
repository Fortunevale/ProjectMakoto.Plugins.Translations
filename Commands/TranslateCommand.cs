// Project Makoto
// Copyright (C) 2023  Fortunevale
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY

using DisCatSharp;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using Newtonsoft.Json;
using ProjectMakoto.Entities.Translation;
using ProjectMakoto.Plugins.Translation;
using ProjectMakoto.Util;
using Xorog.UniversalExtensions;

namespace ProjectMakoto.Commands;

internal sealed class TranslateCommand : BaseCommand
{
    public override Task ExecuteCommand(SharedCommandContext ctx, Dictionary<string, object> arguments)
    {
        return Task.Run(async () =>
        {
            var CommandKey = ((Plugins.Translation.Entities.Translations)TranslationPlugin.Plugin!.Translations).Commands.TranslateMessage;

            DiscordMessage bMessage;

            if (arguments?.ContainsKey("message") ?? false)
            {
                bMessage = (DiscordMessage)arguments["message"];
            }
            else
            {
                switch (ctx.CommandType)
                {
                    case Enums.CommandType.PrefixCommand:
                    {
                        if (ctx.OriginalCommandContext.Message.ReferencedMessage is not null)
                        {
                            bMessage = ctx.OriginalCommandContext.Message.ReferencedMessage;
                        }
                        else
                        {
                            this.SendSyntaxError();
                            return;
                        }

                        break;
                    }
                    default:
                        throw new ArgumentException("Message expected");
                }
            }

            if (await ctx.DbUser.Cooldown.WaitForModerate(ctx))
                return;

            var transSource = bMessage.Content;
            transSource = RegexTemplates.Url.Replace(transSource, "");

            if (transSource.IsNullOrWhiteSpace())
            {
                _ = await this.RespondOrEdit(new DiscordMessageBuilder().WithEmbed(new DiscordEmbedBuilder
                {
                    Description = this.GetString(CommandKey.NoContent, true),
                }.AsError(ctx)));
                return;
            }

            HttpClient client = new();

            client.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/103.0.5060.114 Safari/537.36");

            var GoogleButton = new DiscordButtonComponent(ButtonStyle.Primary, Guid.NewGuid().ToString(), "Google", false, new DiscordComponentEmoji(DiscordEmoji.FromGuildEmote(ctx.Client, 1001098467550179469)));
            var LibreTranslateButton = new DiscordButtonComponent(ButtonStyle.Primary, Guid.NewGuid().ToString(), "LibreTranslate", false, new DiscordComponentEmoji(DiscordEmoji.FromGuildEmote(ctx.Client, 1001098468602945598)));

            _ = await this.RespondOrEdit(new DiscordMessageBuilder().WithEmbed(new DiscordEmbedBuilder
            {
                Description = this.GetString(CommandKey.SelectProvider, true),
            }.AsAwaitingInput(ctx)).AddComponents(new List<DiscordComponent> { GoogleButton, LibreTranslateButton }).AddComponents(MessageComponents.GetCancelButton(ctx.DbUser, ctx.Bot)));

            var e = await ctx.WaitForButtonAsync(TimeSpan.FromMinutes(1));

            if (e.TimedOut)
            {
                this.ModifyToTimedOut();
                return;
            }

            _ = e.Result.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);

            if (e.GetCustomId() == GoogleButton.CustomId)
            {
                _ = await this.RespondOrEdit(new DiscordMessageBuilder().WithEmbed(new DiscordEmbedBuilder
                {
                    Description = this.GetString(CommandKey.SelectSource, true),
                }.AsAwaitingInput(ctx)));

                var SourceResult = await this.PromptCustomSelection(ctx.Bot.LanguageCodes.List.Select(x => new DiscordStringSelectComponentOption(x.Name, x.Code, null!, x.Code == TranslationPlugin.Plugin.UserData![ctx.User.Id].LastGoogleSource)).ToList(), this.GetString(CommandKey.SelectSourceDropdown));

                if (SourceResult.TimedOut)
                {
                    this.ModifyToTimedOut();
                    return;
                }
                else if (SourceResult.Cancelled)
                {
                    this.DeleteOrInvalidate();
                    return;
                }
                else if (SourceResult.Errored)
                {
                    throw SourceResult.Exception;
                }

                TranslationPlugin.Plugin.UserData![ctx.User.Id].LastGoogleSource = SourceResult.Result;

                _ = await this.RespondOrEdit(new DiscordMessageBuilder().WithEmbed(new DiscordEmbedBuilder
                {
                    Description = this.GetString(CommandKey.SelectTarget, true,
                        new TVar("Source", SourceResult.Result)),
                }.AsAwaitingInput(ctx)));

                var TargetResult = await this.PromptCustomSelection(ctx.Bot.LanguageCodes.List.Where(x => x.Code != "auto").Select(x => new DiscordStringSelectComponentOption(x.Name, x.Code, null!, x.Code == TranslationPlugin.Plugin.UserData![ctx.User.Id].LastGoogleTarget)).ToList(), this.GetString(CommandKey.SelectTargetDropdown));

                if (TargetResult.TimedOut)
                {
                    this.ModifyToTimedOut();
                    return;
                }
                else if (TargetResult.Cancelled)
                {
                    this.DeleteOrInvalidate();
                    return;
                }
                else if (TargetResult.Errored)
                {
                    throw TargetResult.Exception;
                }

                TranslationPlugin.Plugin.UserData![ctx.User.Id].LastGoogleTarget = TargetResult.Result;

                _ = await this.RespondOrEdit(new DiscordMessageBuilder().WithEmbed(new DiscordEmbedBuilder
                {
                    Description = this.GetString(CommandKey.Translating, true),
                }.AsLoading(ctx)));

                var TranslationTask = TranslationPlugin.Plugin!.TranslationClient!.Translate(SourceResult.Result, TargetResult.Result, transSource);

                var PosInQueue = TranslationPlugin.Plugin!.TranslationClient.Queue.Count;

                var Announced = false;
                var Wait = 0;

                while (!TranslationTask.IsCompleted)
                {
                    if (Wait > 3 && !Announced)
                    {
                        Announced = true;

                        _ = await this.RespondOrEdit(new DiscordMessageBuilder().WithEmbed(new DiscordEmbedBuilder
                        {
                            Description = this.GetString(CommandKey.Queue).Build(true, new TVar("Position", PosInQueue), new TVar("Timestamp", Formatter.Timestamp(TranslationPlugin.Plugin!.TranslationClient.LastRequest.AddSeconds(PosInQueue * 10)))),
                        }.AsLoading(ctx)));
                    }

                    Wait++;
                    await Task.Delay(1000);
                }

                var Translation = TranslationTask.Result;

                _ = await this.RespondOrEdit(new DiscordMessageBuilder().WithEmbed(new DiscordEmbedBuilder
                {
                    Description = $"{Translation.Item1}",
                }.AsInfo(ctx, "", this.GetString(CommandKey.Translated,
                    new TVar("Source", SourceResult.Result == "auto" ? $"{ctx.Bot.LanguageCodes.List.First(x => x.Code == Translation.Item2).Name} (Auto)" : ctx.Bot.LanguageCodes.List.First(x => x.Code == SourceResult.Result).Name),
                    new TVar("Target", ctx.Bot.LanguageCodes.List.First(x => x.Code == TargetResult.Result).Name),
                    new TVar("Provider", "Google")))));
            }
            else if (e.GetCustomId() == LibreTranslateButton.CustomId)
            {
                var languagesResponse = await client.GetAsync($"http://{TranslationPlugin.Plugin!.LoadedConfig.LibreTranslateHost}/languages");

                var TranslationTargets = JsonConvert.DeserializeObject<List<LibreTranslateLanguage>>(await languagesResponse.Content.ReadAsStringAsync());

                var TranslationSources = TranslationTargets!.ToList();
                TranslationSources.Insert(0, new LibreTranslateLanguage { code = "auto", name = "Auto Detect (experimental)" });

                _ = await this.RespondOrEdit(new DiscordMessageBuilder().WithEmbed(new DiscordEmbedBuilder
                {
                    Description = this.GetString(CommandKey.SelectSource, true),
                }.AsAwaitingInput(ctx)));

                var SourceResult = await this.PromptCustomSelection(TranslationSources.Select(x => new DiscordStringSelectComponentOption(x.name!, x.code!, null!, x.code == TranslationPlugin.Plugin.UserData![ctx.User.Id].LastLibreTranslateSource)).ToList(), this.GetString(CommandKey.SelectSourceDropdown));

                if (SourceResult.TimedOut)
                {
                    this.ModifyToTimedOut();
                    return;
                }
                else if (SourceResult.Cancelled)
                {
                    this.DeleteOrInvalidate();
                    return;
                }
                else if (SourceResult.Errored)
                {
                    throw SourceResult.Exception;
                }

                TranslationPlugin.Plugin.UserData![ctx.User.Id].LastLibreTranslateSource = SourceResult.Result;

                _ = await this.RespondOrEdit(new DiscordMessageBuilder().WithEmbed(new DiscordEmbedBuilder
                {
                    Description = this.GetString(CommandKey.SelectTarget, true,
                        new TVar("Source", SourceResult.Result)),
                }.AsAwaitingInput(ctx)));

                var TargetResult = await this.PromptCustomSelection(TranslationTargets!.Select(x => new DiscordStringSelectComponentOption(x.name!, x.code!, null!, x.code == TranslationPlugin.Plugin.UserData![ctx.User.Id].LastLibreTranslateTarget)).ToList(), this.GetString(CommandKey.SelectTargetDropdown));

                if (TargetResult.TimedOut)
                {
                    this.ModifyToTimedOut();
                    return;
                }
                else if (TargetResult.Cancelled)
                {
                    this.DeleteOrInvalidate();
                    return;
                }
                else if (TargetResult.Errored)
                {
                    throw TargetResult.Exception;
                }

                TranslationPlugin.Plugin.UserData![ctx.User.Id].LastLibreTranslateTarget = TargetResult.Result;

                _ = await this.RespondOrEdit(new DiscordMessageBuilder().WithEmbed(new DiscordEmbedBuilder
                {
                    Description = this.GetString(CommandKey.Translating, true),
                }.AsLoading(ctx)));

                string query;

                using (var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "q", transSource },
                    { "source", SourceResult.Result },
                    { "target", TargetResult.Result },
                }))
                {
                    query = await content.ReadAsStringAsync();
                }

                var translateResponse = await client.PostAsync($"http://{TranslationPlugin.Plugin!.LoadedConfig.LibreTranslateHost}/translate?{query}", null);
                var parsedTranslation = JsonConvert.DeserializeObject<LibreTranslateTranslation>(await translateResponse.Content.ReadAsStringAsync());

                _ = await this.RespondOrEdit(new DiscordMessageBuilder().WithEmbed(new DiscordEmbedBuilder
                {
                    Description = $"{parsedTranslation!.translatedText}",
                }.AsInfo(ctx, "", this.GetString(CommandKey.Translated,
                    new TVar("Source", (SourceResult.Result == "auto" ? $"{TranslationSources.First(x => x.code == parsedTranslation.detectedLanguage!.language).name} ({parsedTranslation.detectedLanguage!.confidence:N0}%)" : TranslationSources.First(x => x.code == SourceResult.Result).name)!),
                    new TVar("Target", TranslationTargets!.First(x => x.code == TargetResult.Result).name!),
                    new TVar("Provider", "LibreTranslate")))));
            }
        });
    }
}
﻿using System;
using System.Threading.Tasks;
using Discord.Commands;
using Tadmor.Extensions;
using Tadmor.Preconditions;
using Tadmor.Services.Textgen;

namespace Tadmor.Modules
{
    [Summary("text generation")]
    public class TextgenModule : ModuleBase<ICommandContext>
    {
        private readonly TextgenService _textgen;
        private static readonly Random Random = new Random();

        public TextgenModule(TextgenService textgen)
        {
            _textgen = textgen;
        }

        [RequireWhitelist]
        [Summary("generates text based on a trained model")]
        [Command("gen")]
        public async Task Generate([Remainder] double? temperature = null)
        {
            var nonNullTemperature = temperature ?? Random.NextDouble() / 10 * 8 + .2;
            var clampedTemperature = Math.Clamp(0, nonNullTemperature, 1);
            var text = await _textgen.Generate(clampedTemperature);
            await Context.Channel.SendMessageAsync(text);
        }
    }
}
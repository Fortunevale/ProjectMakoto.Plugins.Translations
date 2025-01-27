// Project Makoto
// Copyright (C) 2023  Fortunevale
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY

namespace ProjectMakoto.Plugins.Translation.Entities;


internal sealed class LibreTranslateTranslation
{
    public string? translatedText { get; set; }
    public DetectedLanguage? detectedLanguage { get; set; }

    internal sealed class DetectedLanguage
    {
        public decimal? confidence { get; set; }
        public string? language { get; set; }
    }
}

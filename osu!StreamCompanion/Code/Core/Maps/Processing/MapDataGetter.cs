﻿using System;
using System.Collections.Generic;
using System.Linq;
using osu_StreamCompanion.Code.Core.Savers;
using StreamCompanionTypes;
using StreamCompanionTypes.DataTypes;
using StreamCompanionTypes.Enums;
using StreamCompanionTypes.Interfaces;

namespace osu_StreamCompanion.Code.Core.Maps.Processing
{
    public class MainMapDataGetter
    {
        private readonly List<IMapDataFinder> _mapDataFinders;
        private readonly List<IMapDataParser> _mapDataParsers;
        private readonly List<IMapDataGetter> _mapDataGetters;
        private List<ITokensProvider> _mapDataReplacementsGetters;

        private readonly MainSaver _saver;
        private ILogger _logger;
        private readonly Settings _settings;
        private readonly SettingNames _names = SettingNames.Instance;

        public MainMapDataGetter(List<IMapDataFinder> mapDataFinders, List<IMapDataGetter> mapDataGetters,
            List<IMapDataParser> mapDataParsers, List<ITokensProvider> mapDataReplacementsGetters,
            MainSaver saver, ILogger logger, Settings settings)
        {
            _mapDataFinders = mapDataFinders.OrderByDescending(x => x.Priority).ToList();
            _mapDataParsers = mapDataParsers;
            _mapDataGetters = mapDataGetters;
            _mapDataReplacementsGetters = mapDataReplacementsGetters;
            _saver = saver;
            _logger = logger;
            _settings = settings;
        }

        public MapSearchResult FindMapData(MapSearchArgs searchArgs)
        {
            MapSearchResult mapSearchResult = null;
            ModsEx foundMods = null;
            for (int i = 0; i < _mapDataFinders.Count; i++)
            {
                if ((_mapDataFinders[i].SearchModes & searchArgs.Status) == 0)
                    continue;
                try
                {
                    mapSearchResult = _mapDataFinders[i].FindBeatmap(searchArgs);

                }
                catch (Exception e)
                {
                    _logger.Log(e, LogLevel.Error);
                    mapSearchResult = null;
                }

                if (mapSearchResult?.FoundBeatmaps == true)
                {
                    if (mapSearchResult.Mods == null && foundMods != null)
                        mapSearchResult.Mods = foundMods;
                    _logger.Log(string.Format(">Found data using \"{0}\" ID: {1}", _mapDataFinders[i].SearcherName, mapSearchResult.BeatmapsFound[0]?.MapId), LogLevel.Advanced);
                    break;
                }
                if (mapSearchResult?.Mods != null)
                    foundMods = mapSearchResult.Mods;
            }
            if (mapSearchResult == null)
                mapSearchResult = new MapSearchResult(searchArgs);

            return mapSearchResult;
        }

        public void ProcessMapResult(MapSearchResult mapSearchResult)
        {
            CreateMapReplacements(mapSearchResult);

            var tokens = new Tokens();
            foreach (var token in Tokens.AllTokens)
            {
                tokens.Add(token.Key, token.Value);
            }

            mapSearchResult.FormatedStrings = GetMapPatterns(tokens, mapSearchResult.Action);


            if (!_settings.Get<bool>(_names.DisableDiskPatternWrite))
                SaveMapStrings(mapSearchResult.FormatedStrings, mapSearchResult.Action);
            SetNewMap(mapSearchResult);
        }

        private void CreateMapReplacements(MapSearchResult mapSearchResult)
        {
            foreach (var mapDataReplacementsGetter in _mapDataReplacementsGetters)
            {
                mapDataReplacementsGetter.CreateTokens(mapSearchResult);
            }
        }

        private void SaveMapStrings(List<OutputPattern> patterns, OsuStatus status)
        {

            foreach (var p in patterns)
            {
                if (!p.IsMemoryFormat)
                {
                    if ((p.SaveEvent & status) != 0)
                        _saver.Save(p.Name + ".txt", p.GetFormatedPattern());
                    else
                        _saver.Save(p.Name + ".txt", "");

                }
            }
        }


        private List<OutputPattern> GetMapPatterns(Tokens replacements, OsuStatus status)
        {
            var ret = new List<OutputPattern>();
            foreach (var dataGetter in _mapDataParsers)
            {
                var temp = dataGetter.GetFormatedPatterns(replacements, status);
                if (temp?.Count > 0)
                {
                    ret.AddRange(temp);
                }
            }
            return ret;
        }

        private void SetNewMap(MapSearchResult map)
        {
            foreach (var dataGetter in _mapDataGetters)
            {
                dataGetter.SetNewMap(map);
            }
        }
    }
}

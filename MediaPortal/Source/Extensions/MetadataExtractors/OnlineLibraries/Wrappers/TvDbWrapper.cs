#region Copyright (C) 2007-2015 Team MediaPortal

/*
    Copyright (C) 2007-2015 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Extensions.OnlineLibraries.Libraries.Common;
using MediaPortal.Extensions.OnlineLibraries.Libraries.TvdbLib;
using MediaPortal.Extensions.OnlineLibraries.Libraries.TvdbLib.Cache;
using MediaPortal.Extensions.OnlineLibraries.Libraries.TvdbLib.Data;
using MediaPortal.Extensions.OnlineLibraries.Libraries.TvdbLib.Data.Banner;
using MediaPortal.Common.MediaManagement.Helpers;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;
using MediaPortal.Extensions.UserServices.FanArtService.Interfaces;

namespace MediaPortal.Extensions.OnlineLibraries.Wrappers
{
  class TvDbWrapper : ApiWrapper<TvdbBanner, TvdbLanguage>
  {
    protected TvdbHandler _tvdbHandler;

    /// <summary>
    /// Sets the preferred language in short format like: en, de, ...
    /// </summary>
    /// <param name="langShort">Short language</param>
    public void SetPreferredLanguage(string langShort)
    {
      TvdbLanguage language = _tvdbHandler.Languages.Find(l => l.Abbriviation == langShort);
      if(language != null)
        SetPreferredLanguage(language);
    }

    /// <summary>
    /// Initializes the library. Needs to be called at first.
    /// </summary>
    /// <returns></returns>
    public bool Init(string cachePath)
    {
      ICacheProvider cacheProvider = new XmlCacheProvider(cachePath);
      _tvdbHandler = new TvdbHandler(cacheProvider);
      _tvdbHandler.InitCache();
      if (!_tvdbHandler.IsLanguagesCached)
        _tvdbHandler.ReloadLanguages();
      _tvdbHandler.UpdateFinished += TvdbHandlerOnUpdateFinished;
      _tvdbHandler.UpdateProgressed += TvdbHandlerOnUpdateProgressed;
      SetDefaultLanguage(TvdbLanguage.DefaultLanguage);
      SetCachePath(cachePath);
      return true;
    }

    private void TvdbHandlerOnUpdateFinished(TvdbHandler.UpdateFinishedEventArgs args)
    {
      ServiceRegistration.Get<ILogger>().Debug("TvDbWrapper: Finished updating cache from {0} to {1}", args.UpdateStarted, args.UpdateFinished);
      ServiceRegistration.Get<ILogger>().Debug("TvDbWrapper: Updated {0} Series, {1} Episodes, {2} Banners.", args.UpdatedSeries.Count, args.UpdatedEpisodes.Count, args.UpdatedBanners.Count);
    }

    private void TvdbHandlerOnUpdateProgressed(TvdbHandler.UpdateProgressEventArgs args)
    {
      ServiceRegistration.Get<ILogger>().Debug("TvDbWrapper: ... {0} {2}. Total: {3}", args.CurrentUpdateStage, args.CurrentStageProgress, args.CurrentUpdateDescription, args.OverallProgress);
    }

    #region Search

    public override bool SearchSeriesEpisode(EpisodeInfo episodeSearch, TvdbLanguage language, out List<EpisodeInfo> episodes)
    {
      language = language ?? PreferredLanguage;

      episodes = null;
      SeriesInfo seriesSearch = null;
      if (episodeSearch.SeriesTvdbId <= 0)
      {
        seriesSearch = episodeSearch.CloneBasicSeries();
        if (!SearchSeriesUniqueAndUpdate(seriesSearch, language))
          return false;
      }

      if (episodeSearch.SeriesTvdbId > 0 && episodeSearch.SeasonNumber.HasValue)
      {
        TvdbSeries seriesDetail = _tvdbHandler.GetSeries(episodeSearch.SeriesTvdbId, language, true, false, false);

        foreach (TvdbEpisode episode in seriesDetail.Episodes)
        {
          if (episodeSearch.EpisodeNumbers.Contains(episode.EpisodeNumber) || episodeSearch.EpisodeNumbers.Count == 0)
          {
            if (episodes == null)
              episodes = new List<EpisodeInfo>();

            EpisodeInfo info = new EpisodeInfo()
            {
              TvdbId = episode.Id,
              SeriesName = new LanguageText(seriesDetail.SeriesName, false),
              SeasonNumber = episode.SeasonNumber,
              EpisodeName = new LanguageText(episode.EpisodeName, false),
            };
            info.EpisodeNumbers.Add(episode.EpisodeNumber);
            info.CopyIdsFrom(seriesSearch);
            info.Languages.Add(episode.Language.Abbriviation);
            episodes.Add(info);
          }
        }
      }

      if (episodes == null)
      {
        episodes = new List<EpisodeInfo>();
        EpisodeInfo info = new EpisodeInfo()
        {
          SeriesName = seriesSearch.SeriesName,
          SeasonNumber = episodeSearch.SeasonNumber,
          EpisodeName = episodeSearch.EpisodeName,
        };
        info.CopyIdsFrom(seriesSearch);
        info.EpisodeNumbers.AddRange(episodeSearch.EpisodeNumbers);
        info.Languages = seriesSearch.Languages;
        episodes.Add(info);
        return true;
      }

      return episodes != null;
    }

    public override bool SearchSeries(SeriesInfo seriesSearch, TvdbLanguage language, out List<SeriesInfo> series)
    {
      language = language ?? PreferredLanguage;

      series = null;
      List<TvdbSearchResult> foundSeries = _tvdbHandler.SearchSeries(seriesSearch.SeriesName.Text, language);
      if (foundSeries == null) return false;
      series = foundSeries.Select(s => new SeriesInfo()
      {
        TvdbId = s.Id,
        ImdbId = s.ImdbId,
        SeriesName = new LanguageText(s.SeriesName, false),
        FirstAired = s.FirstAired,
        Languages = new List<string>(new string[] { s.Language.Abbriviation })
      }).ToList();

      return series.Count > 0;
    }

    #endregion

    #region Update

    public override bool UpdateFromOnlineSeries(SeriesInfo series, TvdbLanguage language, bool cacheOnly)
    {
      language = language ?? PreferredLanguage;

      TvdbSeries seriesDetail = null;
      if (series.TvdbId > 0)
        seriesDetail = _tvdbHandler.GetSeries(series.TvdbId, language, true, true, false);
      if (seriesDetail == null && !cacheOnly && !string.IsNullOrEmpty(series.ImdbId))
      {
        TvdbSearchResult foundSeries = _tvdbHandler.GetSeriesByRemoteId(ExternalId.ImdbId, series.ImdbId);
        if (foundSeries != null)
        {
          seriesDetail = _tvdbHandler.GetSeries(foundSeries.Id, language, true, true, false);
        }
      }
      if (seriesDetail == null) return false;

      series.TvdbId = seriesDetail.Id;
      series.ImdbId = seriesDetail.ImdbId;

      series.SeriesName = new LanguageText(seriesDetail.SeriesName, false);
      series.FirstAired = seriesDetail.FirstAired;
      series.Description = new LanguageText(seriesDetail.Overview, false);
      series.Certification = seriesDetail.ContentRating;
      series.TotalRating = seriesDetail.Rating;
      series.RatingCount = seriesDetail.RatingCount;
      series.Genres = seriesDetail.Genre;
      series.Networks = ConvertToCompanies(seriesDetail.NetworkID, seriesDetail.Network, CompanyAspect.COMPANY_TV_NETWORK);
      if (seriesDetail.Status.IndexOf("Ended", StringComparison.InvariantCultureIgnoreCase) >= 0)
      {
        series.IsEnded = true;
      }

      series.Actors = ConvertToPersons(seriesDetail.TvdbActors, PersonAspect.OCCUPATION_ACTOR);
      series.Characters = ConvertToCharacters(seriesDetail.TvdbActors);

      TvdbEpisode nextEpisode = seriesDetail.Episodes.Where(e => e.FirstAired > DateTime.Now).FirstOrDefault();
      if (nextEpisode != null)
      {
        series.NextEpisodeName = nextEpisode.EpisodeName;
        series.NextEpisodeAirDate = nextEpisode.FirstAired;
        series.NextEpisodeSeasonNumber = nextEpisode.SeasonNumber;
        series.NextEpisodeNumber = nextEpisode.EpisodeNumber;
      }

      return true;
    }

    public override bool UpdateFromOnlineSeriesSeason(SeasonInfo season, TvdbLanguage language, bool cacheOnly)
    {
      language = language ?? PreferredLanguage;

      TvdbSeries seriesDetail = null;
      if (season.SeriesTvdbId > 0)
        seriesDetail = _tvdbHandler.GetSeries(season.SeriesTvdbId, language, true, false, false);
      if (seriesDetail == null && !cacheOnly && !string.IsNullOrEmpty(season.SeriesImdbId))
      {
        TvdbSearchResult foundSeries = _tvdbHandler.GetSeriesByRemoteId(ExternalId.ImdbId, season.SeriesImdbId);
        if (foundSeries != null)
        {
          seriesDetail = _tvdbHandler.GetSeries(foundSeries.Id, language, true, false, false);
        }
      }
      if (seriesDetail == null) return false;
      if (!season.SeasonNumber.HasValue)
        return false;
      var episode = seriesDetail.Episodes.Where(e => e.SeasonNumber == season.SeasonNumber).ToList().FirstOrDefault();
      if (episode == null)
        return false;

      season.TvdbId = episode.SeasonId;
      season.SeriesTvdbId = seriesDetail.Id;
      season.SeriesImdbId = seriesDetail.ImdbId;
      season.FirstAired = episode.FirstAired;

      season.SeriesName = new LanguageText(seriesDetail.SeriesName, false);
      season.FirstAired = seriesDetail.FirstAired;
      season.SeasonNumber = season.SeasonNumber.Value;
      season.Description = new LanguageText(seriesDetail.Overview, false);

      return true;
    }

    public override bool UpdateFromOnlineSeriesEpisode(EpisodeInfo episode, TvdbLanguage language, bool cacheOnly)
    {
      language = language ?? PreferredLanguage;

      List<EpisodeInfo> episodeDetails = new List<EpisodeInfo>();
      TvdbSeries seriesDetail = null;
      TvdbEpisode episodeDetail = null;

      if (episode.SeriesTvdbId > 0 && episode.SeasonNumber.HasValue && episode.EpisodeNumbers.Count > 0)
      {
        seriesDetail = _tvdbHandler.GetSeries(episode.SeriesTvdbId, language, true, true, false);
        if (seriesDetail == null && !cacheOnly && !string.IsNullOrEmpty(episode.SeriesImdbId))
        {
          TvdbSearchResult foundSeries = _tvdbHandler.GetSeriesByRemoteId(ExternalId.ImdbId, episode.SeriesImdbId);
          if (foundSeries != null)
          {
            seriesDetail = _tvdbHandler.GetSeries(foundSeries.Id, language, true, true, false);
          }
        }
        if (seriesDetail == null) return false;

        foreach (int episodeNumber in episode.EpisodeNumbers)
        {
          episodeDetail = seriesDetail.Episodes.Where(e => e.EpisodeNumber == episodeNumber && e.SeasonNumber == episode.SeasonNumber.Value).FirstOrDefault();
          if (episodeDetail == null) return false;

          EpisodeInfo info = new EpisodeInfo()
          {
            TvMazeId = episodeDetail.Id,

            SeriesTvdbId = seriesDetail.Id,
            SeriesImdbId = seriesDetail.ImdbId,
            SeriesName = new LanguageText(seriesDetail.SeriesName, false),
            SeriesFirstAired = seriesDetail.FirstAired,

            TvdbId = episodeDetail.Id,
            ImdbId = episodeDetail.ImdbId,
            SeasonNumber = episodeDetail.SeasonNumber,
            EpisodeNumbers = new List<int>(new int[] { episodeDetail.EpisodeNumber }),
            DvdEpisodeNumbers = new List<double>(new double[] { episodeDetail.DvdEpisodeNumber }),
            FirstAired = episodeDetail.FirstAired,
            EpisodeName = new LanguageText(episodeDetail.EpisodeName, false),
            Summary = new LanguageText(episodeDetail.Overview, false),
            Genres = seriesDetail.Genre,
            TotalRating = episodeDetail.Rating,
            RatingCount = episodeDetail.RatingCount,
          };

          info.Actors = ConvertToPersons(seriesDetail.TvdbActors, PersonAspect.OCCUPATION_ACTOR);
          info.Actors.AddRange(ConvertToPersons(episodeDetail.GuestStars, PersonAspect.OCCUPATION_ACTOR, info.Actors.Count));
          info.Characters = ConvertToCharacters(seriesDetail.TvdbActors);
          info.Directors = ConvertToPersons(episodeDetail.Directors, PersonAspect.OCCUPATION_DIRECTOR, 0);
          info.Writers = ConvertToPersons(episodeDetail.Writer, PersonAspect.OCCUPATION_WRITER, 0);
          info.Languages.Add(episodeDetail.Language.Abbriviation);

          episodeDetails.Add(info);
        }
      }

      if (episodeDetails.Count > 1)
      {
        SetMultiEpisodeDetails(episode, episodeDetails);
        return true;
      }
      else if (episodeDetails.Count > 0)
      {
        SetEpisodeDetails(episode, episodeDetails[0]);
        return true;
      }
      return false;
    }

    #endregion

    #region Convert

    private List<PersonInfo> ConvertToPersons(List<TvdbActor> actors, string occupation)
    {
      if (actors == null || actors.Count == 0)
        return new List<PersonInfo>();

      int sortOrder = 0;
      List<PersonInfo> retValue = new List<PersonInfo>();
      foreach (TvdbActor person in actors)
        retValue.Add(new PersonInfo() { TvdbId = person.Id, Name = person.Name, Occupation = occupation, Order = sortOrder++ });
      return retValue;
    }

    private List<PersonInfo> ConvertToPersons(List<string> actors, string occupation, int offset)
    {
      if (actors == null || actors.Count == 0)
        return new List<PersonInfo>();

      int sortOrder = offset;
      List<PersonInfo> retValue = new List<PersonInfo>();
      foreach (string person in actors)
        retValue.Add(new PersonInfo() { Name = person, Occupation = occupation, Order = sortOrder++ });
      return retValue;
    }

    private List<CompanyInfo> ConvertToCompanies(int companyID, string company, string type)
    {
      if (string.IsNullOrEmpty(company))
        return new List<CompanyInfo>();

      int sortOrder = 0;
      return new List<CompanyInfo>(new CompanyInfo[]
      {
        new CompanyInfo()
        {
          TvdbId = companyID > 0 ? companyID : 0,
          Name = company,
          Type = type,
          Order = sortOrder++
        }
      });
    }

    private List<CharacterInfo> ConvertToCharacters(List<TvdbActor> actors)
    {
      if (actors == null || actors.Count == 0)
        return new List<CharacterInfo>();

      int sortOrder = 0;
      List<CharacterInfo> retValue = new List<CharacterInfo>();
      foreach (TvdbActor person in actors)
        retValue.Add(new CharacterInfo()
        {
          ActorTvdbId = person.Id,
          ActorName = person.Name,
          Name = person.Role,
          Order = sortOrder++
        });
      return retValue;
    }

    #endregion

    #region FanArt

    public override bool GetFanArt<T>(T infoObject, TvdbLanguage language, string scope, out ApiWrapperImageCollection<TvdbBanner> images)
    {
      images = new ApiWrapperImageCollection<TvdbBanner>();
      TvdbSeries seriesDetail = null;
      language = language ?? PreferredLanguage;

      if (scope == FanArtMediaTypes.Series)
      {
        EpisodeInfo episode = infoObject as EpisodeInfo;
        SeasonInfo season = infoObject as SeasonInfo;
        SeriesInfo series = infoObject as SeriesInfo;
        if (series == null && season != null)
        {
          series = season.CloneBasicSeries();
        }
        if (series == null && episode != null)
        {
          series = episode.CloneBasicSeries();
        }
        if (series != null && series.TvdbId > 0)
        {
          seriesDetail = _tvdbHandler.GetSeries(series.TvdbId, language, false, true, true);

          if (seriesDetail != null)
          {
            try
            {
              //Save all actors here because they are saved under the series
              foreach (TvdbActorBanner banner in seriesDetail.TvdbActors.Select(a => a.ActorImage).ToList())
              {
                banner.LoadBanner();
                banner.UnloadBanner();
              }
            }
            catch(Exception ex)
            {
              ServiceRegistration.Get<ILogger>().Error("TvDbWrapper: Error downloading acter banners", ex);
            }

            images.Id = series.TvdbId.ToString();
            images.Posters.AddRange(seriesDetail.PosterBanners);
            images.Banners.AddRange(seriesDetail.SeriesBanners);
            images.Backdrops.AddRange(seriesDetail.FanartBanners);
            return true;
          }
        }
      }
      else if (scope == FanArtMediaTypes.SeriesSeason)
      {
        EpisodeInfo episode = infoObject as EpisodeInfo;
        SeasonInfo season = infoObject as SeasonInfo;
        if (season == null && episode != null)
        {
          season = episode.CloneBasicSeason();
        }
        if (season != null && season.SeriesTvdbId > 0 && season.SeasonNumber.HasValue)
        {
          seriesDetail = _tvdbHandler.GetSeries(episode.SeriesTvdbId, language, false, false, true);

          if (seriesDetail != null)
          {
            images.Id = episode.TvdbId.ToString();

            var seasonLookup = seriesDetail.SeasonBanners.Where(s => s.Season == season.SeasonNumber).ToLookup(s => string.Format("{0}_{1}", s.Season, s.BannerType), v => v);
            foreach (IGrouping<string, TvdbSeasonBanner> tvdbSeasonBanners in seasonLookup)
            {
              images.Banners.AddRange(seasonLookup[tvdbSeasonBanners.Key]);
            }
            return true;
          }
        }
      }
      else if (scope == FanArtMediaTypes.Episode)
      {
        EpisodeInfo episode = infoObject as EpisodeInfo;
        if (episode != null && episode.SeriesTvdbId > 0 && episode.SeasonNumber.HasValue && episode.EpisodeNumbers.Count > 0)
        {
          seriesDetail = _tvdbHandler.GetSeries(episode.SeriesTvdbId, language, true, false, true);

          if (seriesDetail != null)
          {
            images.Id = episode.TvdbId.ToString();

            TvdbEpisode episodeDetail = seriesDetail.Episodes.Find(e => e.SeasonNumber == episode.SeasonNumber.Value && e.EpisodeNumber == episode.EpisodeNumbers[0]);
            if(episodeDetail != null)
              images.Banners.AddRange(new TvdbBanner[] { episodeDetail.Banner });
            return true;
          }
        }
      }
      else if(scope == FanArtMediaTypes.Actor)
      {
        // Probably already downloaded for the series
        return true;
      }
      else
      {
        return true;
      }
      return false;
    }

    public override bool DownloadFanArt(string id, TvdbBanner image, string scope, string type)
    {
      image.LoadBanner();
      return image.UnloadBanner();
    }

    public override bool DownloadSeriesSeasonFanArt(string id, int seasonNo, TvdbBanner image, string scope, string type)
    {
      image.LoadBanner();
      return image.UnloadBanner();
    }

    public override bool DownloadSeriesEpisodeFanArt(string id, int seasonNo, int episodeNo, TvdbBanner image, string scope, string type)
    {
      image.LoadBanner();
      return image.UnloadBanner();
    }

    #endregion

    #region Cache

    /// <summary>
    /// Updates the local available information with updated ones from online source.
    /// </summary>
    /// <returns></returns>
    public bool UpdateCache()
    {
      try
      {
        return _tvdbHandler.UpdateAllSeries(true);
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error("TvDbWrapper: Error updating cache", ex);
        return false;
      }
    }

    #endregion
  }
}

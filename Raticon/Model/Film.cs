﻿using System;
using System.IO.Abstractions;
using Raticon.Service;
using System.Linq;

namespace Raticon.Model
{
    public interface IFilm
    {
        string ImdbId { get; }
        string Rating { get; }
        string Title { get; }
        string Year { get; }
        string Poster { get; }
    }
    public interface IFilmFromFolder : IFilm
    {
        string Path { get; }
        string FolderName { get; }
        string PathTo(string fileName);
    }
    public abstract class AbstractFilm : IFilm
    {
        public string ImdbId { get; protected set; }
        public string Rating { get; protected set; }
        public string Title { get; protected set; }
        public string Year { get; protected set; }
        public string Poster { get; protected set; }
    }
    public abstract class AbstractFilmFromFolder : AbstractFilm, IFilmFromFolder
    {
        public string Path { get; protected set; }
        public string FolderName { get; protected set; }

        public string PathTo(string fileName)
        {
            return System.IO.Path.Combine(Path, fileName);
        }
    }
    public abstract class FilmFromApi : IFilm
    {
        protected string imdbIdCache;
        public string ImdbId
        {
            get
            {
                if (imdbIdCache == null)
                {
                    setImdbFromService();
                }
                return imdbIdCache;
            }
        }

        public void FetchData()
        {
            FetchData(delegate { });
        }
        public void FetchData(Action onComplete)
        {
            setImdbFromService(() => updateRatingResultCache(onComplete));
        }

        protected void setImdbFromService()
        {
            setImdbFromService(delegate { });
        }
        protected abstract void setImdbFromService(Action onComplete);

        protected RatingResult ratingResultCache;
        protected T getResult<T>(T default_value, Func<RatingResult, T> getProperty)
        {
            if (ratingResultCache == null && ImdbId != null)
            {
                updateRatingResultCache();
            }

            if (ratingResultCache == null)
            {
                return default_value;
            }

            return getProperty(ratingResultCache);
        }


        protected void updateRatingResultCache()
        {
            updateRatingResultCache(delegate { });
        }
        protected virtual void updateRatingResultCache(Action onComplete)
        {
            ratingResultCache = getRatingSafe();
            onComplete();
        }

        protected RatingResult getRatingSafe()
        {
            if(ImdbId == null)
            {
                return null;
            }

            try
            {
                return ratingService.GetRating(ImdbId);
            }
            catch (System.Net.WebException)
            {
                return null;
            }
        }

        public string Rating
        {
            get { return getResult("", r => r.Rating); }
        }
        public string Title
        {
            get { return getResult("", r => r.Title); }
        }
        public string Year
        {
            get { return getResult("", r => r.Year); }
        }
        public string Poster
        {
            get { return getResult("", r => r.Poster); }
        }

        protected IFileSystem fileSystem;
        protected IRatingService ratingService;
        protected IResultPicker resultPicker;
        protected FilmLookupService idLookupService;

        public FilmFromApi(IFileSystem fileSystem = null, IRatingService ratingService = null, IResultPicker resultPicker = null)
        {
            this.fileSystem = fileSystem ?? new FileSystem();
            this.ratingService = ratingService ?? new RatingService();
            this.resultPicker = resultPicker ?? new FirstResultPicker();

            this.idLookupService = new FilmLookupService();
        }
    }

    public class FilmFromFolder : FilmFromApi, IFilmFromFolder
    {
        public virtual string Path { get; protected set; }
        public virtual string FolderName { get; protected set; }

        public FilmFromFolder(string path, IFileSystem fileSystem, IRatingService ratingService = null, IResultPicker resultPicker = null) : base(fileSystem, ratingService, resultPicker)
        {
            this.Path = path;
            FolderName = this.fileSystem.Path.GetFileName(path);
        }

        public string PathTo(string fileName)
        {
            return System.IO.Path.Combine(Path, fileName);
        }

        protected override void setImdbFromService(Action onComplete)
        {
            imdbIdCache = idLookupService.Lookup(FolderName, (l) => resultPicker.Pick(l));
            onComplete();
        }
    }

    public class CachedFilm : FilmFromFolder
    {
        public CachedFilm(string path, IFileSystem fileSystem, IRatingService ratingService, IResultPicker resultPicker = null) : base(path, fileSystem, ratingService, resultPicker)
        {
            this.idLookupService = new CachingFilmLookupService(Path, fileSystem);
        }

        public CachedFilm(string path, IFileSystem fileSystem = null, IResultPicker resultPicker = null) : this(path, fileSystem, new DiskCachedRatingService(), resultPicker)
        {
        }
    }

}

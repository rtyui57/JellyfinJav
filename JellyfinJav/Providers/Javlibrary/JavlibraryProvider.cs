using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Entities;
using System.Web;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace JellyfinJav.Providers.JavlibraryProvider
{
    public class JavlibraryProvider : IRemoteMetadataProvider<Movie, MovieInfo>, IHasOrder
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private readonly ILibraryManager libraryManager;
        private readonly ILogger<JavlibraryProvider> logger;
        private static readonly Api.JavlibraryClient client = new Api.JavlibraryClient();

        public string Name => "Javlibrary";
        public int Order => 11;

        public JavlibraryProvider(ILibraryManager libraryManager,
                                  ILogger<JavlibraryProvider> logger)
        {
            this.libraryManager = libraryManager;
            this.logger = logger;
        }

        public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancelToken)
        {
            var originalTitle = Utility.GetVideoOriginalTitle(info, libraryManager);

            logger.LogInformation("[JellyfinJav] Javlibrary - Scanning: " + originalTitle);

            Api.Video? result = null;
            if (info.ProviderIds.ContainsKey("Javlibrary"))
                result = await client.LoadVideo(info.ProviderIds["Javlibrary"]);
            else
                result = await client.SearchFirst(
                    Utility.ExtractCodeFromFilename(originalTitle)
                );

            if (!result.HasValue)
                return new MetadataResult<Movie>();

            return new MetadataResult<Movie>
            {
                Item = new Movie
                {
                    OriginalTitle = originalTitle,
                    Name = Utility.CreateVideoDisplayName(result.Value),
                    ProviderIds = new Dictionary<string, string> { { "Javlibrary", result.Value.Id } },
                    Studios = new[] { result.Value.Studio }.OfType<string>().ToArray(),
                    Genres = result.Value.Genres.ToArray()
                },
                People = CreateActressList(result.Value),
                HasMetadata = true
            };
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo info, CancellationToken cancelToken)
        {
            return from video in await client.Search(info.Name)
                   select new RemoteSearchResult
                   {
                       Name = video.code,
                       ProviderIds = new Dictionary<string, string>
                       {
                           // Functionality should be instead moved into the javlibrary lib
                           { "Javlibrary", HttpUtility.ParseQueryString(video.url.Query).Get("v") }
                       }
                   };
        }

        public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancelToken)
        {
            return await httpClient.GetAsync(url).ConfigureAwait(false);
        }

        private static string NormalizeActressName(string name)
        {
            if (Plugin.Instance.Configuration.actressNameOrder == ActressNameOrder.FirstLast)
                return string.Join(" ", name.Split(' ').Reverse());
            return name;
        }

        private static List<PersonInfo> CreateActressList(Api.Video video)
        {
            if (!Plugin.Instance.Configuration.enableActresses)
                return new List<PersonInfo>();

            return (from actress in video.Actresses
                    select new PersonInfo
                    {
                        Name = NormalizeActressName(actress),
                        Type = PersonType.Actor
                    }).ToList();
        }
    }
}
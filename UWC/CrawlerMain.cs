using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Dom.Css;
using AngleSharp.Parser.Css;
using AngleSharp.Parser.Html;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Resources;
using System.Threading;
using System.Threading.Tasks;
using UWC.Classes;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using AngleSharp;

namespace UWC
{

    public class CrawlerMain
    {
        private List<string> lstNotAllowedSchemes = new List<string>();

        private bool _ParseAsync;
        public bool ParseAsync
        {
            get
            {
                return _ParseAsync;
            }
        }

        private int _MaxDepth = 1;
        public int MaxDepth
        {
            get
            {
                return _MaxDepth;
            }
        }

        private int _MaxTrds = 10;
        public int MaxThreads
        {
            get
            {
                return _MaxTrds;
            }
        }

        public Uri BaseUrl { get; set; }

        private CrawlerPage _CurrentPage;
        public CrawlerPage CurrentPage
        {
            get
            {
                return _CurrentPage;
            }
        }

        private ObservableCollection<CrawlerPage> _PagesCrawled = new ObservableCollection<CrawlerPage>();
        public ObservableCollection<CrawlerPage> PagesCrawled
        {
            get
            {
                return _PagesCrawled;
            }
        }

        CancellationTokenSource cts; //Declare a cancellation token source
        List<IBrowsingContext> _lstBrowsingContexts = null;

        public CrawlerMain(int _MaxThreads, bool _DoParseAsync)
        {
            lstNotAllowedSchemes.Add("mailto");
            lstNotAllowedSchemes.Add("skype");
            lstNotAllowedSchemes.Add("none");

            // Instantiate the CancellationTokenSource.
            cts = new CancellationTokenSource();

            _lstBrowsingContexts = new List<IBrowsingContext>();
            // Setup the configuration to support document loading
            var config = Configuration.Default.WithDefaultLoader();

            for (int iThreads = 0; iThreads < _MaxThreads; iThreads++)
            {
                var bc = BrowsingContext.New(config);
                _lstBrowsingContexts.Add(bc);
            }
            _MaxTrds = _MaxThreads;
            _ParseAsync = _DoParseAsync;

            _PagesCrawled.CollectionChanged += _PagesCrawled_CollectionChanged;
        }

        void _PagesCrawled_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            string sYees = "";
        }
        

        /// <summary>
        /// To start extracting web content from one Root Page.
        /// </summary>
        /// <param name="_pagetocrawl">The CrawlerPage with the URL from where to start.</param>
        /// <returns></returns>
        async Task StartExtration(CrawlerPage _pagetocrawl)
        {
            _CurrentPage = _pagetocrawl;
            Task<CrawlerPage> t = ExtractContent(_pagetocrawl, cts.Token);
            // awaits the first page to be crawled as there are no other links
            CrawlerPage pg = await t;
            pg.Level = 0;
            PagesCrawled.Add(pg);
            if(_MaxDepth > pg.Level)
                await ContinueExtraction(pg, cts.Token);
        }

        async Task ContinueExtraction(CrawlerPage _CurrPg, CancellationToken _ct)
        {
            // ***Create a query that, when executed, returns a collection of tasks.
            IEnumerable<Url> _lst = _CurrPg.Links.Where(sl0 => !PagesCrawled.Select(slPC => slPC.PageUrl).Contains(sl0) && !lstNotAllowedSchemes.Contains(sl0.Scheme));

            try
            {
                // ***Use ToList to execute the query and start the tasks. 
                List<Task<CrawlerPage>> downloadTasks = (from uri in _lst.Take(MaxThreads)
                                                         select ExtractContent(new CrawlerPage(uri.Href, ParseAsync) { Level = _CurrPg.Level + 1 }, cts.Token)).ToList();
                // ***Add a loop to process the tasks one at a time until none remain.
                while (downloadTasks.Count > 0 && downloadTasks.Count <= MaxThreads && !_ct.IsCancellationRequested)
                {
                    // Identify the first task that completes.
                    Task<CrawlerPage> firstFinishedTask = await Task.WhenAny(downloadTasks);

                    // ***Remove the selected task from the list so that you don't
                    // process it more than once.
                    downloadTasks.Remove(firstFinishedTask);
                    
                    // Await the completed task.
                    CrawlerPage crawledPage = await firstFinishedTask;
                    _CurrentPage = crawledPage;
                    if (!PagesCrawled.Any(sl0 => sl0.PageUrl == crawledPage.PageUrl))
                        PagesCrawled.Add(crawledPage);
                }

                if (_CurrPg.Links.Where(sl0 => !PagesCrawled.Select(slPC => slPC.PageUrl).Contains(sl0) && !lstNotAllowedSchemes.Contains(sl0.Scheme)).Count() > 0)
                {
                    if (_MaxDepth > _CurrPg.Level)
                        await ContinueExtraction(_CurrPg, _ct);
                }
                else
                {
                    List<CrawlerPage> lstPagesToCrawl =
                        PagesCrawled.Where(slF => slF.Links.Any(sl0 => !PagesCrawled.Select(slPC => slPC.PageUrl).Contains(sl0) && !lstNotAllowedSchemes.Contains(sl0.Scheme))).ToList();
                    if (lstPagesToCrawl.Count > 0 && _MaxDepth > lstPagesToCrawl.First().Level)
                    {
                        CrawlerPage newpage = lstPagesToCrawl.First();
                        await ContinueExtraction(lstPagesToCrawl.First(), _ct);
                    }

                }
            }
            catch(AggregateException aEx)
            {
                string error = aEx.Message;
            }
        }

        /// <summary>
        /// Extracts, asyncronously, the content of a CrawlerPage given its URL.
        /// </summary>
        /// <param name="_Page">The CrawlerPage</param>
        /// <param name="_ct">Cancellation Token</param>
        /// <returns></returns>
        async Task<CrawlerPage> ExtractContent(CrawlerPage _Page, CancellationToken _ct)
        {
            try
            {
                var document = await _lstBrowsingContexts[0].OpenAsync(_Page.PageUrl.AbsoluteUri);
                if (document.ContentType.StartsWith("text"))
                {
                    _Page.Document = document;
                    return _Page;
                }
                else
                {
                    var httpClient = new HttpClient();
                    var response = await httpClient.GetByteArrayAsync(_Page.PageUrl);
                    _Page.Binary = response;
                    _Page.Extension = document.ContentType.Substring(document.ContentType.LastIndexOf("/")).Replace("/", "");
                    _Page.Status = CrawlerPage.PageStatus.loaded;
                    return _Page;

                }
            }
            catch (HttpRequestException respex)
            {
                _Page.Content = respex.Message;
                _Page.Status = CrawlerPage.PageStatus.error;
                return _Page;
            }
        }

        /// <summary>
        /// Use this to start Crawling pages.
        /// </summary>
        /// <param name="_StartingPage">The URL from where to start</param>
        /// <param name="_depth">How many levels of extraction will be done. 1 is all the links of the first page only, 2 is the links of the links of the firsta page, and so on.</param>
        public async void Start(string _StartingPage, int _depth)
        {
            _MaxDepth = _depth;
            try
            {
                cts.Dispose();
                cts = new CancellationTokenSource();
                TaskFactory tf = new TaskFactory(cts.Token);
                await StartExtration(new CrawlerPage(_StartingPage, _ParseAsync));
            }
            catch(TaskCanceledException ex)
            {

            }
            catch(Exception ex)
            {

            }
        }

        public void Stop()
        {
            cts.Cancel();
        }

        public static void SavePage(CrawlerPage _cp, string _path)
        {
            FileInfo fi = new FileInfo(_path);
            if (fi.Exists)
                fi.Delete();
            if(!string.IsNullOrEmpty( _cp.sHTML))
            {
                StreamWriter sw = fi.CreateText();
                sw.Write(_cp.sHTML);
                sw.Dispose();
            }
            else if(_cp.Binary != null && _cp.Binary.Length > 0)
            {
                FileStream fs = fi.Create();
                fs.Write(_cp.Binary, 0, _cp.Binary.Length);
                fs.Dispose();
            }
        }

    }
}

using AngleSharp.Dom;
using AngleSharp.Parser.Html;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace WSCP.C.Classes
{
    public class CrawlerPage : INotifyPropertyChanged
    {
        public enum PageStatus { requested, loaded, parsed, error }

        /// <summary>
        /// Get the status of the page, Say
        /// </summary>
        private PageStatus _status;
        public PageStatus Status
        {
            get { return _status; }
            set
            {
                _status = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// A unique Id for the page. BIG number, just in case.
        /// </summary>
        public Int64 IdPage { get; set; }

        /// <summary>
        /// The site level, related to the Root of th Page Crawled.
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// When it was first parsed.
        /// </summary>
        private DateTime _DateFirstLoaded;
        public DateTime DateFirstLoaded
        {
            get
            {
                return _DateFirstLoaded;
            }
        }

        /// <summary>
        /// The Url of the page.
        /// </summary>
        public Uri PageUrl { get; set; }

        /// <summary>
        /// Will extract the root of the site automatically.
        /// </summary>
        public Uri RootUrl
        {
            get
            {
                if (PageUrl != null && PageUrl.IsWellFormedOriginalString())
                {
                    string baseUrl = PageUrl.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped);
                    var userinfo = PageUrl.GetComponents(UriComponents.UserInfo, UriFormat.UriEscaped);
                    return new Uri(baseUrl);
                }
                return null;
            }
        }

        /// <summary>
        /// We will keep the entire HTML code of the page. Temporarily.
        /// </summary>
        private String _sHTML;
        public String sHTML
        {
            get { return _sHTML; }
            set
            {
                if (string.IsNullOrEmpty(_sHTML))
                    _DateFirstLoaded = DateTime.Now;
                _sHTML = value;
                Status = PageStatus.loaded;

                RaisePropertyChanged();

                if (DoAsyncParsing)
                    DoParsingTask();
                else
                    ParseHTML();
            }
        }

        /// <summary>
        /// We will keep the binary content of non text content (media).
        /// </summary>
        private byte[] _Binary;
        public byte[] Binary
        {
            get { return _Binary; }
            set
            {
                if (_Binary != null &&_Binary.Length <= 0)
                    _DateFirstLoaded = DateTime.Now;
                _Binary = value;
                Status = PageStatus.loaded;

                RaisePropertyChanged();
            }
        }

        private string _Extention;
        public string Extention
        {
            get
            {
                return _Extention;
            }
            set
            { _Extention = value; }
        }


        /// <summary>
        /// The page title
        /// </summary>
        public String Title { get; set; }

        /// <summary>
        /// Here will go the content of the page, filtered from the header/footer, ads, other related links, etc.
        /// </summary>
        private String _Content;
        public String Content
        {
            get { return _Content; }
            set
            {
                _Content = value;
                Status = PageStatus.loaded;

                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// If the site is like Wordpres, Joomla, Drupal, etc. We put this information here.
        /// </summary>
        public string SiteType { get; set; }

        /// <summary>
        /// Gets the list of found links.
        /// </summary>
        private List<Uri> _Links = new List<Uri>();
        public List<Uri> Links
        {
            get
            {
                return _Links;
            }
        }

        public bool DoAsyncParsing { get; set; }

        public static Uri Sanitize(String url)
        {
            Uri uri;

            if (File.Exists(url))
                url = "file://localhost/" + url.Replace('\\', '/');

            var lurl = url.ToLower();

            if (!lurl.StartsWith("file://") && !lurl.StartsWith("http://") && !lurl.StartsWith("https://"))
                url = "http://" + url;

            if (Uri.TryCreate(url, UriKind.Absolute, out uri))
                return uri;

            return new Uri("http://www.google.com/search?q=" + url);
        }

        public CrawlerPage(string _URL, bool _DoAsnyncParsing)
        {
            DoAsyncParsing = _DoAsnyncParsing;
            PageUrl = Sanitize(_URL);
        }

        public void ParseHTML()
        {
            try
            {
                var parser = new HtmlParser();
                var document = parser.Parse(sHTML);

                //automatically gets the other page details
                Title = document.Title;
                SiteType = document.ContentType;
                Content = document.Body.TextContent.Replace("\n\n", "\n");
                var links = document.GetElementsByTagName("a").Select(sl0 => sl0.Attributes.Where(sl1 => sl1.Name == "href").ToList()).ToList();
                List<Uri> lstUrls = new List<Uri>();

                foreach (var elm in document.QuerySelectorAll("a"))
                {
                    if (elm.GetAttribute("href") != null)
                        if (elm.GetAttribute("href").Contains("http:"))
                            lstUrls.Add(new Uri(elm.GetAttribute("href"), UriKind.RelativeOrAbsolute));
                        else
                            lstUrls.Add(new Uri(PageUrl, elm.GetAttribute("href")));
                }
                _Links = lstUrls;
                RaisePropertyChanged();

                Status = PageStatus.parsed;
            }
            catch
            {
                Status = PageStatus.error;
            }
        }

        private void DoParsingTask()
        {
            Task.Factory.StartNew(() => ParseHTML());
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName] String name = null)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }

    }
}

using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace UWC.Classes
{
    public class CrawlerPage : INotifyPropertyChanged
    {
        public enum PageStatus { requested, loaded, parsed, error }

        private IDocument _Document;
        /// <summary>
        /// The AngleSharp document model, use it to get all the data
        /// </summary>
        public IDocument Document
        {
            get { return _Document; }
            set
            {
                _Document = value;
                RaisePropertyChanged();

                if (string.IsNullOrEmpty(_sHTML))
                    _DateFirstLoaded = DateTime.Now;

                Title = _Document.Title;
                sHTML = _Document.ToHtml();
                Content = _Document.TextContent;
                _Links = _Document.Links.Select(sl => new Url(((IHtmlAnchorElement)sl).Href)).Distinct().ToList();
                _Images = _Document.Images.Select(sl=> sl.Source).Distinct().ToList();

                Status = PageStatus.parsed;
            }
        }
        private PageStatus _status;
        /// <summary>
        /// Get the status of the page, Say
        /// </summary>
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

        private DateTime _DateFirstLoaded;
        /// <summary>
        /// When it was first parsed.
        /// </summary>
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

        private String _sHTML;
        /// <summary>
        /// We will keep the entire HTML code of the page. Temporarily.
        /// </summary>
        public String sHTML
        {
            get { return _sHTML; }
            set
            {
                //if (string.IsNullOrEmpty(_sHTML))
                //    _DateFirstLoaded = DateTime.Now;
                _sHTML = value;
                //Status = PageStatus.loaded;

                //RaisePropertyChanged();

                //if (DoAsyncParsing)
                //    DoParsingTask();
                //else
                //    ParseHTML();
            }
        }

        private byte[] _Binary;
        /// <summary>
        /// We will keep the binary content of non text content (media).
        /// </summary>
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

        private string _Extension;
        /// <summary>
        /// The extencion of the archive
        /// </summary>
        public string Extension
        {
            get
            {
                return _Extension;
            }
            set
            { _Extension = value; }
        }


        /// <summary>
        /// The page title
        /// </summary>
        public String Title { get; set; }

        private String _Content;
        /// <summary>
        /// Here will go the content of the page, filtered from the header/footer, ads, other related links, etc.
        /// </summary>
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

        public string ContentPreview
        {
            get
            {
                if (!string.IsNullOrEmpty(_Content))
                    return _Content.Substring(0, 100);
                else
                    return "";
            }
        }

        /// <summary>
        /// If the site is like Wordpres, Joomla, Drupal, etc. We put this information here.
        /// </summary>
        public string SiteType { get; set; }

        private List<Url> _Links = new List<Url>();
        /// <summary>
        /// Gets the list of found links.
        /// </summary>
        public List<Url> Links
        {
            get
            {
                return _Links;
            }
        }

        private List<string> _Images = new List<string> ();
        /// <summary>
        /// Gets the list of found links.
        /// </summary>
        public List<string> Images
        {
            get
            {
                return _Images;
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

            if (lurl.StartsWith("http:\\\\"))
                url = lurl.Replace("http:\\\\", "http://");

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
                _Links = document.Links.Select(sl0=> sl0.BaseUrl).ToList();
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

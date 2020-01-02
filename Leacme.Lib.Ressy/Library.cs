// Copyright (c) 2017 Leacme (http://leac.me). View LICENSE.md for more information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CodeHollow.FeedReader;
using LiteDB;

namespace Leacme.Lib.Ressy {

	public class Library {

		private LiteDatabase db = new LiteDatabase(typeof(Library).Namespace + ".Settings.db");
		private LiteCollection<BsonDocument> settingsCollection;
		private LiteCollection<BsonDocument> urlCollection;

		public Library() {
			settingsCollection = db.GetCollection(nameof(settingsCollection));
			urlCollection = db.GetCollection(nameof(urlCollection));

		}

		/// <summary>
		/// Get the RSS feed via URL.
		/// /// </summary>
		/// <param name="feedUrl"></param>
		/// <returns></returns>
		public async Task<Feed> GetFeed(Uri feedUrl, int timeoutSeconds = 6) {
			Feed feed = null;
			using (var handler = new HttpClientHandler()) {
				handler.ClientCertificateOptions = ClientCertificateOption.Manual;
				handler.ServerCertificateCustomValidationCallback = (z, zz, zzz, zzzz) => { return true; };
				using (HttpClient client = new HttpClient(handler)) {
					client.DefaultRequestHeaders.Add("user-agent", "rss/1.0");
					client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
					using (var st = await client.GetStreamAsync(feedUrl.ToString())) {
						using (var ms = new MemoryStream()) {
							await st.CopyToAsync(ms);
							feed = FeedReader.ReadFromByteArray(ms.ToArray());
						}
					}
				}
			}

			return feed;
		}

		/// <summary>
		/// Check if the database exists.
		/// /// </summary>
		/// <returns></returns>
		public bool DatabaseExists() {
			return db.CollectionExists(nameof(urlCollection)) && db.CollectionExists(nameof(settingsCollection));
		}

		/// <summary>
		///	Open the internet browser executable and navigate to the specified URL.
		/// </summary>
		/// <param name="browserExecutablePath"></param>
		/// <param name="uri"></param>
		/// <param name="optionalArguments"></param>
		public void OpenBrowser(Uri browserExecutablePath, Uri uri, string optionalArguments = "") {
			Process.Start(browserExecutablePath.LocalPath, optionalArguments + " " + uri);
		}

		public void PopulateWithDefaultRssOnDatabaseCreate() {
			if (!(db.CollectionExists(nameof(urlCollection)))) {
				AddRssUrl(new Uri("http://feeds.bbci.co.uk/news/world/rss.xml"));
				AddRssUrl(new Uri("http://feeds.bbci.co.uk/news/business/rss.xml"));
				AddRssUrl(new Uri("http://feeds.bbci.co.uk/news/health/rss.xml"));
			}
		}

		/// <summary>
		/// Store the path to the internet browser executable to open the RSS links.
		/// </summary>
		/// <param name="filepath"></param>
		public void SetBrowserPath(Uri filepath) {
			settingsCollection.Delete(z => z.ContainsKey("Filepath"));
			settingsCollection.Insert(new BsonDocument { ["Filepath"] = filepath.ToString() });
		}

		/// <summary>
		/// Store the path to the internet browser executable arguments to help open the RSS links.
		/// </summary>
		/// <param name="arguments"></param>
		public void SetBrowserArguments(string arguments) {
			settingsCollection.Delete(z => z.ContainsKey("Arguments"));
			if (!string.IsNullOrWhiteSpace(arguments)) {
				settingsCollection.Insert(new BsonDocument { ["Arguments"] = arguments });
			}
		}

		/// <summary>
		/// Store the number of columns to display.
		/// </summary>
		/// <param name="columns"></param>
		public void SetNumberOfColumns(int columns) {
			settingsCollection.Delete(z => z.ContainsKey("NumColumns"));
			settingsCollection.Insert(new BsonDocument { ["NumColumns"] = columns });
		}

		/// <summary>
		/// Get the number of columns to display.
		/// </summary>
		/// <returns></returns>
		public int GetNumberOfColumns() {
			var nc = settingsCollection.FindAll().Where(z => z.ContainsKey("NumColumns")).Select(z => z["NumColumns"]);

			if (nc.Any()) {
				return nc.First();
			} else {
				throw new InvalidOperationException("No columns number preset stored.");
			}
		}

		/// <summary>
		/// Get the path to the internet browser executable to open the RSS links.
		/// /// </summary>
		/// <returns></returns>
		public Uri GetBrowserPath() {
			var fp = settingsCollection.FindAll().Where(z => z.ContainsKey("Filepath")).Select(z => z["Filepath"]);
			if (fp.Any()) {
				return new Uri(fp.First());
			} else {
				throw new InvalidOperationException("No browser executable path is stored.");
			}
		}

		/// <summary>
		/// Get the path to the internet browser executable arguments to help open the RSS links.
		/// /// </summary>
		/// <returns></returns>
		public string GetBrowserArguments() {
			var fp = settingsCollection.FindAll().Where(z => z.ContainsKey("Arguments")).Select(z => z["Arguments"]);
			if (fp.Any()) {
				return fp.First().AsString;
			} else {
				throw new InvalidOperationException("No browser executable path arguments are stored.");
			}
		}

		/// <summary>
		///  Store a RSS feed for future retrieval.
		/// </summary>
		/// <param name="rssUrl"></param>
		public void AddRssUrl(Uri rssUrl) {
			urlCollection.Delete(z => z["RssUrl"].Equals(rssUrl.ToString()));
			urlCollection.Insert(new BsonDocument { ["RssUrl"] = rssUrl.ToString() });
		}

		/// <summary>
		/// Delete a stored RSS feed from the database.
		/// </summary>
		/// <param name="rssUrl"></param>
		public void DeleteRssUrl(Uri rssUrl) {
			urlCollection.Delete(z => z["RssUrl"].Equals(rssUrl.ToString()));
		}

		/// <summary>
		///  Retrieve all stored RSS URLs from the database.
		/// </summary>
		/// <returns></returns>
		public IEnumerable<Uri> GetAllRssUrls() {
			return urlCollection.FindAll().Select(z => new Uri(z["RssUrl"]));
		}

	}
}
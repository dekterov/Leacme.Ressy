// Copyright (c) 2017 Leacme (http://leac.me). View LICENSE.md for more information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CodeHollow.FeedReader;
using Dasync.Collections;
using Leacme.Lib.Ressy;

namespace Leacme.App.Ressy {

	public class AppUI {

		private StackPanel rootPan = (StackPanel)Application.Current.MainWindow.Content;
		private Library lib = new Library();
		private Window optionsWindow;
		private Grid rootGrid = new Grid();
		private ScrollViewer gridScroll = App.ScrollViewer;
		private Dictionary<int, ColumnDefinitions> cdDefs = new Dictionary<int, ColumnDefinitions>();
		private (StackPanel holder, TextBlock label, TextBox field, Button button) brwr;
		private TextBox oeaFl = App.TextBox;

		public AppUI() {

			var blb1 = App.TextBlock;
			blb1.Text = "Personalized RSS Feeds:";

			Dispatcher.UIThread.InvokeAsync(async () => {
				try {
					lib.GetBrowserPath();
				} catch (InvalidOperationException) {
					optionsWindow = InitOptionsWindow();
					await optionsWindow.ShowDialog<Window>(Application.Current.MainWindow);
				}

				await PopulateFeedsAsync();
			});

			DispatcherTimer.Run(() => { Dispatcher.UIThread.InvokeAsync(async () => await PopulateFeedsAsync()); return true; }, TimeSpan.FromMinutes(60));
			try {
				lib.GetNumberOfColumns();
			} catch (InvalidOperationException) {
				lib.SetNumberOfColumns(2);
			}

			lib.PopulateWithDefaultRssOnDatabaseCreate();

			optionsWindow = InitOptionsWindow();
			MenuItem optMenuItem = new MenuItem() { Header = "Options..." };
			optMenuItem.Click += async (z, zz) => {
				if (!Application.Current.Windows.Contains(optionsWindow)) {
					optionsWindow = InitOptionsWindow();
					await optionsWindow.ShowDialog<Window>(Application.Current.MainWindow);
				}
			};
			((AvaloniaList<object>)((MenuItem)((AvaloniaList<object>)((Menu)
			rootPan.Children.First()).Items).First()).Items).Insert(0, optMenuItem);

			gridScroll.Height = App.Current.MainWindow.Height - 105;
			gridScroll.Background = Brushes.Transparent;

			App.Current.MainWindow.PropertyChanged += (z, zz) => {
				if (zz.Property.Equals(Window.HeightProperty)) {
					gridScroll.Height = App.Current.MainWindow.Height - 105;
				}
			};

			var addF = App.HorizontalFieldWithButton;
			addF.holder.HorizontalAlignment = HorizontalAlignment.Center;
			addF.label.Text = "Add RSS Feed:";
			addF.field.Width = 700;
			addF.field.Watermark = "http://example.com/rssfeed.xml";
			addF.button.Content = "Add";
			addF.button.Click += async (z, zz) => {
				if (!string.IsNullOrWhiteSpace(addF.field.Text) && Uri.TryCreate(
					addF.field.Text, UriKind.Absolute, out var uOut) && (
						uOut.Scheme == Uri.UriSchemeHttp || uOut.Scheme == Uri.UriSchemeHttps)) {
					try {
						lib.AddRssUrl(uOut);
						await PopulateFeedsAsync();
						addF.field.Text = string.Empty;
					} catch {
						lib.DeleteRssUrl(uOut);
						await PopulateFeedsAsync();
						addF.field.Text = string.Empty;
					}
				}
			};
			rootPan.Children.AddRange(new List<IControl> { blb1, gridScroll, addF.holder });
		}

		private void PopulateGrid(List<Control> items) {

			var rootGrid = new Grid();
			this.rootGrid = rootGrid;
			gridScroll.Content = this.rootGrid;
			rootGrid.Margin = new Thickness(10, 0);
			rootGrid.Background = Brushes.Transparent;
			rootGrid.HorizontalAlignment = HorizontalAlignment.Stretch;
			cdDefs[1] = new ColumnDefinitions("*");
			cdDefs[2] = new ColumnDefinitions("*,*");
			cdDefs[3] = new ColumnDefinitions("*,*,*");

			rootGrid.ColumnDefinitions = cdDefs[lib.GetNumberOfColumns()];
			int R = (int)Math.Ceiling((decimal)items.Count / lib.GetNumberOfColumns());
			for (int i = 0; i < R; i++) {
				rootGrid.RowDefinitions.Add(new RowDefinition());
			}
			int emtCountr = 0;
			for (int r = 0; r < R; r++) {
				for (int c = 0; c < lib.GetNumberOfColumns(); c++) {
					if (emtCountr < items.Count) {
						items.ElementAt(emtCountr)[Grid.RowProperty] = r;
						items.ElementAt(emtCountr++)[Grid.ColumnProperty] = c;
					}
				}
			}
			rootGrid.Children.AddRange(items);
		}

		private Window InitOptionsWindow() {
			var optWin = App.NotificationWindow;
			optWin.Title = "Options";
			optWin.Width = 500;
			optWin.Height = 250;

			var contPanel = new StackPanel();
			contPanel.VerticalAlignment = VerticalAlignment.Center;
			optWin.Content = contPanel;
			var colNumSettings = App.TextBlock;
			colNumSettings.Text = "Number of display columns:";
			contPanel.Children.Add(colNumSettings);
			var colNumSlide = App.HorizontalSliderWithValue;
			colNumSlide.slider.Minimum = 1;
			colNumSlide.slider.Maximum = 3;
			colNumSlide.slider.Value = lib.GetNumberOfColumns();
			colNumSlide.slider.IsSnapToTickEnabled = true;

			TextBlock colMinBlurb = App.TextBlock;
			colMinBlurb.Text = "columns";
			colNumSlide.holder.Children.Add(colMinBlurb);

			var iBSettings = App.TextBlock;
			iBSettings.Text = "External internet browser:";

			brwr = App.HorizontalFieldWithButton;
			brwr.label.Text = "Executable Path:";
			brwr.field.IsReadOnly = true;
			brwr.field.Width = 300;
			brwr.field.Watermark = "path/to/internet/browser/exetutable.exe";
			brwr.button.Content = "Set...";
			brwr.button.Width = 70;

			var oeaHdr = App.HorizontalStackPanel;
			var oeaLb = App.TextBlock;
			oeaLb.Text = "Optional Exe Arguments:";

			oeaFl = App.TextBox;
			oeaFl.Watermark = "-arg1 -arg2";
			oeaFl.Width = 150;

			oeaHdr.Children.AddRange(new List<IControl> { oeaLb, oeaFl });

			brwr.button.Click += async (z, zz) => {
				var eblPat = await OpenFile();
				if (eblPat.Any()) {
					brwr.field.Text = eblPat.First();
				}
			};

			try {
				brwr.field.Text = lib.GetBrowserPath().LocalPath;
			} catch (InvalidOperationException) {
				//
			}

			try {
				oeaFl.Text = lib.GetBrowserArguments();
			} catch (InvalidOperationException) {
				//
			}

			var confBt = App.Button;
			confBt.Content = "OK";
			confBt.Margin = new Thickness(10);
			confBt.Click += async (z, zz) => {
				lib.SetNumberOfColumns((int)(double)colNumSlide.slider.Value);
				await PopulateFeedsAsync();
				optWin.Close();
			};

			optWin.Closing += (z, zz) => {
				if (string.IsNullOrWhiteSpace(brwr.field.Text)) {
					zz.Cancel = true;
				} else {
					lib.SetBrowserPath(new Uri(brwr.field.Text));
				}

				lib.SetBrowserArguments(oeaFl.Text);

			};
			contPanel.Children.AddRange(new List<IControl> { colNumSlide.holder, new Control { Height = 15 }, iBSettings, brwr.holder, oeaHdr, confBt });
			return optWin;
		}

		private async Task PopulateFeedsAsync() {
			((App)Application.Current).LoadingBar.IsIndeterminate = true;
			var feedsControls = new List<Control>();
			var feeds = new ConcurrentBag<(Feed feed, Uri url)>();
			var urls = lib.GetAllRssUrls().ToList();

			await urls.ParallelForEachAsync(
						async (url) => {
							Feed feed = new Feed();
							try {
								feed = await lib.GetFeed(url);
								feeds.Add((feed, url));
							} catch (TaskCanceledException) {
								feed.Title = "Unable to load feed: " + url;
								feed.Items = new List<FeedItem>();
								feeds.Add((feed, url));
							}
						}, 5);

			foreach (var f in feeds.ToList().OrderBy(z => urls.IndexOf(z.url)).ToList()) {
				var dg = App.DataGrid;
				dg.Height = 200;
				dg.Margin = new Thickness(5);
				dg.SetValue(DataGrid.WidthProperty, AvaloniaProperty.UnsetValue);
				dg.PointerReleased += (z, zz) => {
					if (zz.MouseButton.Equals(MouseButton.Right)) {
						ContextMenu oneM = new ContextMenu();
						MenuItem RemEntryMenuItem = new MenuItem() { Header = "Remove Feed" };
						RemEntryMenuItem.Click += async (zzz, zzzz) => {
							lib.DeleteRssUrl(f.url);
							await PopulateFeedsAsync();
						};
						((AvaloniaList<object>)oneM.Items).Add(RemEntryMenuItem);
						oneM.Open((DataGrid)z);
					}
				};

				dg.CellPointerPressed += (z, zz) => {
					if (zz.Column?.DisplayIndex.Equals(0) == true) {
						if (zz.PointerPressedEventArgs.MouseButton.Equals(MouseButton.Left)) {

							var selectedRow = dg.Items.Cast<dynamic>().ToList().ElementAt(zz.Row.GetIndex());

							string optionalArguments = "";
							if (!string.IsNullOrWhiteSpace(oeaFl.Text)) {
								optionalArguments = oeaFl.Text;
							}
							if (!string.IsNullOrWhiteSpace(selectedRow.Links)) {
								lib.OpenBrowser(new Uri(brwr.field.Text), new Uri(selectedRow.Links), optionalArguments);
							}
						}
					}
				};

				dg.AutoGeneratingColumn += (z, zz) => {
					if (zz.Column.Header.Equals("Links")) {
						zz.Cancel = true;
					} else if (zz.Column.Header.Equals("Title")) {

						if (!string.IsNullOrWhiteSpace(f.feed.Title)) {
							zz.Column.Header = f.feed.Title;
						} else {

							if (f.feed.Items.Any(zzz => zzz.Link != null)) {
								zz.Column.Header = new Uri(f.feed.Items.First(zzz => zzz.Link != null).Link).Host;
							} else {
								zz.Column.Header = "No Title";
							}
						}
					}
				};

				dg.Items = f.feed.Items.Select(z => new { Title = z.Title, Links = z.Link });
				feedsControls.Add(dg);

			}

			PopulateGrid(feedsControls);
			((App)Application.Current).LoadingBar.IsIndeterminate = false;

		}

		private async Task<IEnumerable<string>> OpenFile() {
			var dialog = new OpenFileDialog() {
				Title = "Set Internet Browser Executable...",
				InitialDirectory = Directory.GetCurrentDirectory(),
				AllowMultiple = false,
			};
			var res = await dialog.ShowAsync(Application.Current.MainWindow);
			return (res?.Any() == true) ? res : Enumerable.Empty<string>();
		}
	}
}
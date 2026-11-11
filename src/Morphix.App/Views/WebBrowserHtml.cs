using System.Windows;
using System.Windows.Controls;

namespace Morphix.App.Views;

/// <summary>
/// Attached Property, mit der ein <see cref="WebBrowser"/> einen HTML-Text aus dem ViewModel anzeigt. Das ist reines
/// View-Verhalten (das Steuerelement lässt sich nicht direkt binden) und enthält keine Logik des Berichts.
/// Der Bericht enthält weder Skripte noch Verweise; Navigation zu Webadressen wird zusätzlich blockiert (NFA06).
/// </summary>
public static class WebBrowserHtml
{
    private const string LeereSeite = "<html><head><meta charset=\"utf-8\"></head><body></body></html>";

    /// <summary>Der anzuzeigende HTML-Text.</summary>
    public static readonly DependencyProperty HtmlProperty = DependencyProperty.RegisterAttached(
        "Html", typeof(string), typeof(WebBrowserHtml), new PropertyMetadata("", HtmlGeaendert));

    private static readonly DependencyProperty VerbundenProperty = DependencyProperty.RegisterAttached(
        "Verbunden", typeof(bool), typeof(WebBrowserHtml), new PropertyMetadata(false));

    /// <summary>Liest den HTML-Text.</summary>
    public static string GetHtml(DependencyObject element) => (string)element.GetValue(HtmlProperty);

    /// <summary>Setzt den HTML-Text.</summary>
    public static void SetHtml(DependencyObject element, string wert) => element.SetValue(HtmlProperty, wert);

    private static void HtmlGeaendert(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not WebBrowser browser)
        {
            return;
        }

        if (!(bool)browser.GetValue(VerbundenProperty))
        {
            browser.SetValue(VerbundenProperty, true);
            browser.Navigating += (_, args) =>
            {
                if (args.Uri is { Scheme: "http" or "https" or "ftp" or "file" })
                {
                    args.Cancel = true;
                }
            };
        }

        // Vor dem Laden des Steuerelements ist der Browser noch nicht bereit; dann erst beim Laden anzeigen.
        if (browser.IsLoaded)
        {
            Zeige(browser);
        }
        else
        {
            RoutedEventHandler? beimLaden = null;
            beimLaden = (_, _) =>
            {
                browser.Loaded -= beimLaden;
                Zeige(browser);
            };
            browser.Loaded += beimLaden;
        }
    }

    private static void Zeige(WebBrowser browser)
    {
        var html = GetHtml(browser);
        browser.NavigateToString(string.IsNullOrEmpty(html) ? LeereSeite : html);
    }
}

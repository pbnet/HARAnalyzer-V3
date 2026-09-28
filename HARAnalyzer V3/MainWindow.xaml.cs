using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using IOPath = System.IO.Path;

namespace HARAnalyzer_V3
{
    public partial class MainWindow : Window
    {
        private readonly List<HarItem> allItems = new();
        private string? currentHarFile;

        public MainWindow()
        {
            InitializeComponent();

            // ====================================================
            // WINDOW ICON
            // ====================================================

            LoadWindowIcon();

            StatisticsText.Text =
                "Select a frame to display performance statistics.";

            TimelineTitle.Text = "Session Timeline";
            TimelineSummary.Text = "";

            Loaded += (_, _) => DrawTimeline();
        }

        // ============================================================
        // WINDOW ICON
        // ============================================================

        private void LoadWindowIcon()
        {
            try
            {
                // First try: HARIco.ico next to the executable.
                string iconPath =
                    IOPath.Combine(
                        AppContext.BaseDirectory,
                        "HARIco.ico");

                if (!File.Exists(iconPath))
                {
                    // When running from Visual Studio the executable is
                    // normally in bin\Debug\netX-windows.
                    // Try to locate HARIco.ico in the project directory.
                    DirectoryInfo? directory =
                        new DirectoryInfo(
                            AppContext.BaseDirectory);

                    while (directory != null)
                    {
                        string candidate =
                            IOPath.Combine(
                                directory.FullName,
                                "HARIco.ico");

                        if (File.Exists(candidate))
                        {
                            iconPath = candidate;
                            break;
                        }

                        directory =
                            directory.Parent;
                    }
                }

                if (File.Exists(iconPath))
                {
                    BitmapImage bitmap =
                        new BitmapImage();

                    bitmap.BeginInit();

                    bitmap.UriSource =
                        new Uri(
                            iconPath,
                            UriKind.Absolute);

                    bitmap.CacheOption =
                        BitmapCacheOption.OnLoad;

                    bitmap.EndInit();
                    bitmap.Freeze();

                    Icon = bitmap;
                }
            }
            catch
            {
                // Icon failure must never prevent HAR Analyzer
                // from starting.
            }
        }

        // ============================================================
        // OPEN HAR
        // ============================================================

        private void OpenHar_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Filter =
                    "HAR files (*.har)|*.har|" +
                    "All files (*.*)|*.*",

                Title =
                    "Open HAR file"
            };

            if (dialog.ShowDialog() == true)
            {
                LoadHarFile(
                    dialog.FileName);
            }
        }

        // ============================================================
        // LOAD HAR
        // ============================================================

        private void LoadHarFile(
            string fileName)
        {
            try
            {
                StatusText.Text =
                    "Loading HAR...";

                string json =
                    File.ReadAllText(
                        fileName);

                using JsonDocument document =
                    JsonDocument.Parse(
                        json);

                if (!document.RootElement.TryGetProperty(
                        "log",
                        out JsonElement log) ||

                    !log.TryGetProperty(
                        "entries",
                        out JsonElement entries) ||

                    entries.ValueKind !=
                        JsonValueKind.Array)
                {
                    MessageBox.Show(
                        "Invalid HAR file. " +
                        "The log.entries array was not found.",
                        "HAR Analyzer V3",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    StatusText.Text =
                        "Invalid HAR file.";

                    return;
                }

                allItems.Clear();

                int frame = 0;

                foreach (
                    JsonElement entry
                    in entries.EnumerateArray())
                {
                    if (!entry.TryGetProperty(
                            "request",
                            out JsonElement request) ||

                        !entry.TryGetProperty(
                            "response",
                            out JsonElement response))
                    {
                        continue;
                    }

                    frame++;

                    string method =
                        GetStringProperty(
                            request,
                            "method");

                    string url =
                        GetStringProperty(
                            request,
                            "url");

                    int status =
                        GetIntProperty(
                            response,
                            "status");

                    double time =
                        GetDoubleProperty(
                            entry,
                            "time");

                    string protocol =
                        GetStringProperty(
                            response,
                            "httpVersion");

                    if (string.IsNullOrWhiteSpace(
                            protocol))
                    {
                        protocol =
                            GetStringProperty(
                                request,
                                "httpVersion");
                    }

                    string host = "";

                    if (Uri.TryCreate(
                            url,
                            UriKind.Absolute,
                            out Uri? uri))
                    {
                        host =
                            uri.Host;
                    }

                    string contentType = "";
                    long bodySize = 0;

                    if (response.TryGetProperty(
                            "content",
                            out JsonElement content))
                    {
                        contentType =
                            GetStringProperty(
                                content,
                                "mimeType");

                        bodySize =
                            GetLongProperty(
                                content,
                                "size");
                    }

                    if (bodySize <= 0)
                    {
                        bodySize =
                            GetLongProperty(
                                response,
                                "bodySize");
                    }

                    DateTimeOffset? started =
                        ParseStartedDateTime(
                            entry);

                    HarItem item =
                        new()
                        {
                            FrameNumber =
                                frame,

                            Method =
                                method,

                            Status =
                                status,

                            Protocol =
                                protocol,

                            Host =
                                host,

                            Url =
                                url,

                            ContentType =
                                contentType,

                            BodySizeValue =
                                bodySize,

                            TimeValue =
                                time,

                            StartedDateTime =
                                started,

                            Entry =
                                entry.Clone()
                        };

                    allItems.Add(
                        item);
                }

                currentHarFile =
                    fileName;

                HarGrid.ItemsSource =
                    null;

                HarGrid.ItemsSource =
                    allItems;

                SearchBox.Text =
                    "";

                RequestText.Text =
                    "";

                ResponseText.Text =
                    "";

                BodyText.Text =
                    "";

                StatisticsText.Text =
                    "Select a frame to display performance statistics.";

                FrameCountText.Text =
                    $"Frames: {allItems.Count:N0}";

                VisibleFrameCountText.Text =
                    $"Visible: {allItems.Count:N0}";

                SelectedFrameText.Text =
                    "Selected: none";

                Title =
                    "HAR Analyzer V3 - " +
                    IOPath.GetFileName(
                        fileName);

                StatusText.Text =
                    $"Loaded {allItems.Count:N0} frames.";

                DrawTimeline();
            }
            catch (JsonException ex)
            {
                StatusText.Text =
                    "Invalid HAR file.";

                MessageBox.Show(
                    "The selected file does not contain " +
                    "valid HAR/JSON data.\n\n" +
                    ex.Message,
                    "Invalid HAR",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "Error loading HAR.";

                MessageBox.Show(
                    ex.ToString(),
                    "HAR Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ============================================================
        // SEARCH
        // ============================================================

        private void SearchBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            ApplySearchFilter();
        }

        private void ClearSearch_Click(
            object sender,
            RoutedEventArgs e)
        {
            SearchBox.Clear();
            SearchBox.Focus();
        }

        private void ApplySearchFilter()
        {
            if (HarGrid == null ||
                HarGrid.ItemsSource == null)
            {
                return;
            }

            ICollectionView view =
                CollectionViewSource.GetDefaultView(
                    HarGrid.ItemsSource);

            string search =
                SearchBox?.Text?.Trim()
                ?? "";

            if (string.IsNullOrWhiteSpace(
                    search))
            {
                view.Filter =
                    null;
            }
            else
            {
                view.Filter =
                    obj =>
                    {
                        if (obj is not HarItem item)
                        {
                            return false;
                        }

                        return
                            Contains(
                                item.FrameNumber.ToString(
                                    CultureInfo.InvariantCulture),
                                search) ||

                            Contains(
                                item.Status.ToString(
                                    CultureInfo.InvariantCulture),
                                search) ||

                            Contains(
                                item.Method,
                                search) ||

                            Contains(
                                item.Protocol,
                                search) ||

                            Contains(
                                item.Host,
                                search) ||

                            Contains(
                                item.Url,
                                search) ||

                            Contains(
                                item.ContentType,
                                search) ||

                            Contains(
                                item.BodySize,
                                search) ||

                            Contains(
                                item.Time,
                                search);
                    };
            }

            view.Refresh();

            int visible = 0;

            foreach (object unused in view)
            {
                visible++;
            }

            VisibleFrameCountText.Text =
                $"Visible: {visible:N0}";

            if (string.IsNullOrWhiteSpace(
                    search))
            {
                StatusText.Text =
                    $"Showing all {visible:N0} frames.";
            }
            else
            {
                StatusText.Text =
                    $"Search: {visible:N0} " +
                    $"of {allItems.Count:N0} frames.";
            }

            DrawTimeline();
        }

        private static bool Contains(
            string? value,
            string search)
        {
            return
                !string.IsNullOrEmpty(
                    value) &&

                value.IndexOf(
                    search,
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ============================================================
        // GRID SELECTION
        // ============================================================

        private void HarGrid_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (HarGrid.SelectedItem
                is not HarItem item)
            {
                SelectedFrameText.Text =
                    "Selected: none";

                return;
            }

            SelectedFrameText.Text =
                $"Selected: #{item.FrameNumber}";

            ShowRequest(
                item.Entry);

            ShowResponse(
                item.Entry);

            ShowResponseBody(
                item.Entry);

            ShowStatistics(
                item);

            DrawTimeline();
        }

        // ============================================================
        // REQUEST
        // ============================================================

        private void ShowRequest(
            JsonElement entry)
        {
            if (!entry.TryGetProperty(
                    "request",
                    out JsonElement request))
            {
                RequestText.Text = "";
                return;
            }

            StringBuilder output =
                new();

            string method =
                GetStringProperty(
                    request,
                    "method");

            string url =
                GetStringProperty(
                    request,
                    "url");

            string version =
                GetStringProperty(
                    request,
                    "httpVersion");

            output.AppendLine(
                $"{method} {url} {version}");

            output.AppendLine();

            AppendHeaders(
                output,
                request);

            if (request.TryGetProperty(
                    "queryString",
                    out JsonElement query) &&

                query.ValueKind ==
                    JsonValueKind.Array &&

                query.GetArrayLength() > 0)
            {
                output.AppendLine();

                output.AppendLine(
                    "---------------- QUERY STRING ----------------");

                output.AppendLine();

                foreach (
                    JsonElement p
                    in query.EnumerateArray())
                {
                    output.AppendLine(
                        $"{GetStringProperty(p, "name")} = " +
                        $"{GetStringProperty(p, "value")}");
                }
            }

            if (request.TryGetProperty(
                    "postData",
                    out JsonElement postData))
            {
                output.AppendLine();

                output.AppendLine(
                    "---------------- REQUEST BODY ----------------");

                output.AppendLine();

                string mimeType =
                    GetStringProperty(
                        postData,
                        "mimeType");

                if (!string.IsNullOrWhiteSpace(
                        mimeType))
                {
                    output.AppendLine(
                        $"Content-Type: {mimeType}");

                    output.AppendLine();
                }

                if (postData.TryGetProperty(
                        "text",
                        out JsonElement body))
                {
                    string text =
                        body.GetString()
                        ?? "";

                    string encoding =
                        GetStringProperty(
                            postData,
                            "encoding");

                    if (encoding.Equals(
                            "base64",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            byte[] bytes =
                                Convert.FromBase64String(
                                    text);

                            if (LooksLikeText(
                                    bytes))
                            {
                                text =
                                    Encoding.UTF8.GetString(
                                        bytes);
                            }
                            else
                            {
                                output.AppendLine(
                                    $"[Binary request body: " +
                                    $"{FormatBytes(bytes.Length)}]");

                                RequestText.Text =
                                    output.ToString();

                                return;
                            }
                        }
                        catch
                        {
                            output.AppendLine(
                                "[Unable to decode Base64 request body]");

                            RequestText.Text =
                                output.ToString();

                            return;
                        }
                    }

                    output.AppendLine(
                        PrettyPrintJson(
                            text));
                }
            }

            RequestText.Text =
                output.ToString();
        }

        // ============================================================
        // RESPONSE
        // ============================================================

        private void ShowResponse(
            JsonElement entry)
        {
            if (!entry.TryGetProperty(
                    "response",
                    out JsonElement response))
            {
                ResponseText.Text = "";
                return;
            }

            StringBuilder output =
                new();

            string version =
                GetStringProperty(
                    response,
                    "httpVersion");

            int status =
                GetIntProperty(
                    response,
                    "status");

            string statusText =
                GetStringProperty(
                    response,
                    "statusText");

            output.AppendLine(
                $"{version} {status} {statusText}");

            output.AppendLine();

            AppendHeaders(
                output,
                response);

            if (response.TryGetProperty(
                    "content",
                    out JsonElement content))
            {
                output.AppendLine();

                output.AppendLine(
                    "---------------- CONTENT ----------------");

                output.AppendLine();

                output.AppendLine(
                    "MIME Type: " +
                    GetStringProperty(
                        content,
                        "mimeType"));

                output.AppendLine(
                    "Size: " +
                    FormatBytes(
                        GetLongProperty(
                            content,
                            "size")));
            }

            ResponseText.Text =
                output.ToString();
        }

        // ============================================================
        // RESPONSE BODY
        // ============================================================

        private void ShowResponseBody(
            JsonElement entry)
        {
            BodyText.Text = "";

            if (!entry.TryGetProperty(
                    "response",
                    out JsonElement response))
            {
                BodyText.Text =
                    "(No response)";

                return;
            }

            if (!response.TryGetProperty(
                    "content",
                    out JsonElement content))
            {
                BodyText.Text =
                    "(No response content)";

                return;
            }

            if (!content.TryGetProperty(
                    "text",
                    out JsonElement textElement))
            {
                BodyText.Text =
                    "(No response body recorded in the HAR)";

                return;
            }

            string text =
                textElement.GetString()
                ?? "";

            if (string.IsNullOrEmpty(
                    text))
            {
                BodyText.Text =
                    "(Empty response body)";

                return;
            }

            string encoding =
                GetStringProperty(
                    content,
                    "encoding");

            if (encoding.Equals(
                    "base64",
                    StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    byte[] bytes =
                        Convert.FromBase64String(
                            text);

                    if (LooksLikeText(
                            bytes))
                    {
                        text =
                            Encoding.UTF8.GetString(
                                bytes);
                    }
                    else
                    {
                        BodyText.Text =
                            "Binary response body\n\n" +
                            "MIME Type: " +
                            GetStringProperty(
                                content,
                                "mimeType") +
                            "\nSize: " +
                            FormatBytes(
                                bytes.Length) +
                            "\nEncoding: Base64";

                        return;
                    }
                }
                catch
                {
                    BodyText.Text =
                        "(Unable to decode Base64 response body)";

                    return;
                }
            }

            BodyText.Text =
                PrettyPrintJson(
                    text);
        }

        // ============================================================
        // STATISTICS
        // ============================================================

        private void ShowStatistics(
            HarItem item)
        {
            JsonElement entry =
                item.Entry;

            StringBuilder sb =
                new();

            sb.AppendLine(
                $"FRAME #{item.FrameNumber}");

            sb.AppendLine(
                $"{item.Method} {item.Url}");

            sb.AppendLine();

            sb.AppendLine(
                "ACTUAL PERFORMANCE");

            sb.AppendLine(
                "------------------");

            DateTimeOffset? start =
                ParseStartedDateTime(
                    entry);

            double blocked =
                GetTiming(
                    entry,
                    "blocked");

            double dns =
                GetTiming(
                    entry,
                    "dns");

            double connect =
                GetTiming(
                    entry,
                    "connect");

            double ssl =
                GetTiming(
                    entry,
                    "ssl");

            double send =
                GetTiming(
                    entry,
                    "send");

            double wait =
                GetTiming(
                    entry,
                    "wait");

            double receive =
                GetTiming(
                    entry,
                    "receive");

            double total =
                item.TimeValue;

            double blocked0 =
                Positive(blocked);

            double dns0 =
                Positive(dns);

            double connect0 =
                Positive(connect);

            double send0 =
                Positive(send);

            double wait0 =
                Positive(wait);

            double receive0 =
                Positive(receive);

            DateTimeOffset? clientConnected =
                start;

            DateTimeOffset? clientBeginRequest =
                start;

            DateTimeOffset? gotRequestHeaders =
                start;

            DateTimeOffset? serverConnected =
                AddMs(
                    start,
                    blocked0 +
                    dns0 +
                    connect0);

            DateTimeOffset? clientDoneRequest =
                AddMs(
                    start,
                    blocked0 +
                    dns0 +
                    connect0 +
                    send0);

            DateTimeOffset? serverGotRequest =
                clientDoneRequest;

            DateTimeOffset? serverBeginResponse =
                AddMs(
                    start,
                    blocked0 +
                    dns0 +
                    connect0 +
                    send0 +
                    wait0);

            DateTimeOffset? gotResponseHeaders =
                serverBeginResponse;

            DateTimeOffset? serverDoneResponse =
                AddMs(
                    start,
                    blocked0 +
                    dns0 +
                    connect0 +
                    send0 +
                    wait0 +
                    receive0);

            DateTimeOffset? clientBeginResponse =
                serverBeginResponse;

            DateTimeOffset? clientDoneResponse =
                total >= 0
                    ? AddMs(
                        start,
                        total)
                    : serverDoneResponse;

            AppendTimestamp(
                sb,
                "ClientConnected:",
                clientConnected,
                true);

            AppendTimestamp(
                sb,
                "ClientBeginRequest:",
                clientBeginRequest);

            AppendTimestamp(
                sb,
                "GotRequestHeaders:",
                gotRequestHeaders,
                true);

            AppendTimestamp(
                sb,
                "ClientDoneRequest:",
                clientDoneRequest);

            AppendDuration(
                sb,
                "Determine Gateway:",
                -1);

            AppendDuration(
                sb,
                "DNS Lookup:",
                dns);

            AppendDuration(
                sb,
                "TCP/IP Connect:",
                connect);

            AppendDuration(
                sb,
                "HTTPS Handshake:",
                ssl);

            AppendTimestamp(
                sb,
                "ServerConnected:",
                serverConnected);

            AppendUnavailableTimestamp(
                sb,
                "FiddlerBeginRequest:");

            AppendTimestamp(
                sb,
                "ServerGotRequest:",
                serverGotRequest);

            AppendTimestamp(
                sb,
                "ServerBeginResponse:",
                serverBeginResponse);

            AppendTimestamp(
                sb,
                "GotResponseHeaders:",
                gotResponseHeaders,
                true);

            AppendTimestamp(
                sb,
                "ServerDoneResponse:",
                serverDoneResponse);

            AppendTimestamp(
                sb,
                "ClientBeginResponse:",
                clientBeginResponse);

            AppendTimestamp(
                sb,
                "ClientDoneResponse:",
                clientDoneResponse);

            sb.AppendLine();

            sb.AppendLine(
                $"Overall Elapsed:       " +
                $"{FormatElapsed(total)}");

            sb.AppendLine();

            sb.AppendLine(
                "HAR TIMINGS");

            sb.AppendLine(
                "-----------");

            AppendDuration(
                sb,
                "Blocked:",
                blocked);

            AppendDuration(
                sb,
                "DNS:",
                dns);

            AppendDuration(
                sb,
                "Connect:",
                connect);

            AppendDuration(
                sb,
                "SSL/TLS:",
                ssl);

            AppendDuration(
                sb,
                "Send:",
                send);

            AppendDuration(
                sb,
                "Wait / TTFB:",
                wait);

            AppendDuration(
                sb,
                "Receive:",
                receive);

            sb.AppendLine();

            sb.AppendLine(
                "RESPONSE");

            sb.AppendLine(
                "--------");

            sb.AppendLine(
                $"Status:                {item.Status}");

            sb.AppendLine(
                $"Protocol:              {item.Protocol}");

            sb.AppendLine(
                $"Content-Type:          {item.ContentType}");

            sb.AppendLine(
                $"Body Size:             " +
                $"{FormatBytes(item.BodySizeValue)}");

            sb.AppendLine();

            sb.AppendLine(
                "* HAR-derived approximation.");

            sb.AppendLine(
                "Standard HAR files do not contain " +
                "every Fiddler proxy timestamp.");

            StatisticsText.Text =
                sb.ToString();
        }

        private static void AppendTimestamp(
            StringBuilder sb,
            string name,
            DateTimeOffset? value,
            bool approximate = false)
        {
            string text =
                value.HasValue
                    ? value.Value.ToString(
                        "HH:mm:ss.fff")
                    : "N/A";

            sb.AppendLine(
                $"{name,-23}{text}" +
                $"{(approximate ? " *" : "")}");
        }

        private static void AppendUnavailableTimestamp(
            StringBuilder sb,
            string name)
        {
            sb.AppendLine(
                $"{name,-23}N/A");
        }

        private static void AppendDuration(
            StringBuilder sb,
            string name,
            double value)
        {
            string text =
                value < 0
                    ? "N/A"
                    : $"{value:0.###}ms";

            sb.AppendLine(
                $"{name,-23}{text}");
        }

        // ============================================================
        // TIMELINE
        // ============================================================

        private void TimelineCanvas_SizeChanged(
            object sender,
            SizeChangedEventArgs e)
        {
            DrawTimeline();
        }

        private void DrawTimeline()
        {
            if (TimelineCanvas == null)
            {
                return;
            }

            TimelineCanvas.Children.Clear();

            IEnumerable<HarItem> source;

            if (HarGrid != null &&
                HarGrid.SelectedItems.Count > 1)
            {
                source =
                    HarGrid.SelectedItems
                        .Cast<HarItem>()
                        .OrderBy(
                            x =>
                                x.StartedDateTime);
            }
            else if (
                HarGrid != null &&
                HarGrid.SelectedItem
                    is HarItem selected)
            {
                source =
                    new[]
                    {
                        selected
                    };
            }
            else if (
                HarGrid != null &&
                HarGrid.ItemsSource != null)
            {
                ICollectionView view =
                    CollectionViewSource.GetDefaultView(
                        HarGrid.ItemsSource);

                List<HarItem> visibleItems =
                    new();

                foreach (
                    object obj
                    in view)
                {
                    if (obj is HarItem item)
                    {
                        visibleItems.Add(
                            item);

                        if (visibleItems.Count >= 100)
                        {
                            break;
                        }
                    }
                }

                source =
                    visibleItems;
            }
            else
            {
                source =
                    allItems.Take(
                        100);
            }

            List<HarItem> items =
                source
                    .Where(
                        x =>
                            x.StartedDateTime.HasValue)
                    .ToList();

            if (items.Count == 0)
            {
                TimelineTitle.Text =
                    "Session Timeline";

                TimelineSummary.Text =
                    "No timing data available";

                AddTimelineText(
                    "No startedDateTime information is available.",
                    20,
                    30,
                    13,
                    Brushes.DimGray);

                return;
            }

            DateTimeOffset first =
                items.Min(
                    x =>
                        x.StartedDateTime!.Value);

            DateTimeOffset last =
                items.Max(
                    x =>
                        x.StartedDateTime!.Value
                            .AddMilliseconds(
                                Math.Max(
                                    0,
                                    x.TimeValue)));

            double spanMs =
                Math.Max(
                    1,
                    (last - first)
                        .TotalMilliseconds);

            if (items.Count == 1)
            {
                TimelineTitle.Text =
                    "Single Session Timeline - " +
                    $"Frame #{items[0].FrameNumber}";
            }
            else
            {
                TimelineTitle.Text =
                    $"Timeline - {items.Count} sessions";
            }

            TimelineSummary.Text =
                $"Span: {FormatElapsed(spanMs)}";

            const double leftMargin =
                250;

            const double topMargin =
                50;

            const double rowHeight =
                30;

            const double barHeight =
                16;

            double width =
                Math.Max(
                    TimelineCanvas.ActualWidth,
                    1100);

            double graphWidth =
                Math.Max(
                    500,
                    width -
                    leftMargin -
                    50);

            TimelineCanvas.Height =
                Math.Max(
                    250,
                    topMargin +
                    items.Count *
                    rowHeight +
                    40);

            const int ticks =
                10;

            for (
                int i = 0;
                i <= ticks;
                i++)
            {
                double x =
                    leftMargin +
                    graphWidth *
                    i /
                    ticks;

                Line gridLine =
                    new()
                    {
                        X1 = x,
                        X2 = x,
                        Y1 = 28,

                        Y2 =
                            TimelineCanvas.Height -
                            10,

                        Stroke =
                            Brushes.Gainsboro,

                        StrokeThickness =
                            1
                    };

                TimelineCanvas.Children.Add(
                    gridLine);

                double tickMs =
                    spanMs *
                    i /
                    ticks;

                AddTimelineText(
                    FormatTimelineTick(
                        tickMs),
                    x + 3,
                    6,
                    11,
                    Brushes.DimGray);
            }

            int row =
                0;

            foreach (
                HarItem item
                in items)
            {
                double y =
                    topMargin +
                    row *
                    rowHeight;

                string label =
                    $"#{item.FrameNumber}  " +
                    $"{item.Method}  " +
                    $"{item.Host}";

                AddTimelineText(
                    label,
                    8,
                    y - 2,
                    12,
                    Brushes.Black);

                double offsetMs =
                    (
                        item.StartedDateTime!.Value -
                        first
                    ).TotalMilliseconds;

                double x =
                    leftMargin +
                    (
                        offsetMs /
                        spanMs
                    ) *
                    graphWidth;

                double barWidth =
                    Math.Max(
                        2,
                        (
                            Math.Max(
                                1,
                                item.TimeValue
                            ) /
                            spanMs
                        ) *
                        graphWidth);

                Rectangle bar =
                    new()
                    {
                        Width =
                            barWidth,

                        Height =
                            barHeight,

                        Fill =
                            GetTimelineBrush(
                                item.ContentType),

                        Stroke =
                            Brushes.DimGray,

                        StrokeThickness =
                            0.5,

                        ToolTip =
                            $"Frame #{item.FrameNumber}\n" +
                            $"{item.Method} {item.Url}\n" +
                            $"Status: {item.Status}\n" +
                            $"Start: " +
                            $"{item.StartedDateTime:HH:mm:ss.fff}\n" +
                            $"Duration: " +
                            $"{item.TimeValue:0.###} ms"
                    };

                Canvas.SetLeft(
                    bar,
                    x);

                Canvas.SetTop(
                    bar,
                    y);

                TimelineCanvas.Children.Add(
                    bar);

                double blocked =
                    Positive(
                        GetTiming(
                            item.Entry,
                            "blocked"));

                double dns =
                    Positive(
                        GetTiming(
                            item.Entry,
                            "dns"));

                double connect =
                    Positive(
                        GetTiming(
                            item.Entry,
                            "connect"));

                double send =
                    Positive(
                        GetTiming(
                            item.Entry,
                            "send"));

                double wait =
                    Positive(
                        GetTiming(
                            item.Entry,
                            "wait"));

                double firstByteOffset =
                    blocked +
                    dns +
                    connect +
                    send +
                    wait;

                if (firstByteOffset >= 0 &&
                    firstByteOffset <=
                        item.TimeValue)
                {
                    double markerX =
                        x +
                        (
                            firstByteOffset /
                            spanMs
                        ) *
                        graphWidth;

                    Line marker =
                        new()
                        {
                            X1 =
                                markerX,

                            X2 =
                                markerX,

                            Y1 =
                                y - 3,

                            Y2 =
                                y +
                                barHeight +
                                3,

                            Stroke =
                                Brushes.Red,

                            StrokeThickness =
                                1.5,

                            ToolTip =
                                "First response byte: " +
                                $"{firstByteOffset:0.###} ms"
                        };

                    TimelineCanvas.Children.Add(
                        marker);
                }

                row++;
            }
        }

        private void AddTimelineText(
            string text,
            double x,
            double y,
            double size,
            Brush brush)
        {
            TextBlock block =
                new()
                {
                    Text =
                        text,

                    FontSize =
                        size,

                    Foreground =
                        brush,

                    FontFamily =
                        new FontFamily(
                            "Segoe UI")
                };

            Canvas.SetLeft(
                block,
                x);

            Canvas.SetTop(
                block,
                y);

            TimelineCanvas.Children.Add(
                block);
        }

        private static Brush GetTimelineBrush(
            string contentType)
        {
            string type =
                (contentType ?? "")
                .ToLowerInvariant();

            if (type.Contains(
                    "javascript"))
            {
                return
                    Brushes.Goldenrod;
            }

            if (type.Contains(
                    "css"))
            {
                return
                    Brushes.SteelBlue;
            }

            if (type.StartsWith(
                    "image/"))
            {
                return
                    Brushes.MediumSeaGreen;
            }

            if (type.Contains(
                    "json"))
            {
                return
                    Brushes.MediumPurple;
            }

            if (type.Contains(
                    "html"))
            {
                return
                    Brushes.CornflowerBlue;
            }

            return
                Brushes.LightSkyBlue;
        }

        private static string FormatTimelineTick(
            double milliseconds)
        {
            if (milliseconds >= 1000)
            {
                return
                    $"{milliseconds / 1000.0:0.##}s";
            }

            return
                $"{milliseconds:0}ms";
        }

        // ============================================================
        // SAZ -> HAR
        // ============================================================

        private async void ConvertSaz_Click(
            object sender,
            RoutedEventArgs e)
        {
            OpenFileDialog openDialog =
                new()
                {
                    Filter =
                        "Fiddler SAZ files (*.saz)|*.saz|" +
                        "All files (*.*)|*.*",

                    Title =
                        "Select Fiddler SAZ file"
                };

            if (openDialog.ShowDialog() != true)
            {
                return;
            }

            string suggestedName =
                IOPath.GetFileNameWithoutExtension(
                    openDialog.FileName) +
                ".har";

            SaveFileDialog saveDialog =
                new()
                {
                    Filter =
                        "HAR files (*.har)|*.har",

                    Title =
                        "Save converted HAR file",

                    FileName =
                        suggestedName,

                    DefaultExt =
                        ".har",

                    AddExtension =
                        true,

                    InitialDirectory =
                        IOPath.GetDirectoryName(
                            openDialog.FileName)
                };

            if (saveDialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                StatusText.Text =
                    "Converting SAZ to HAR...";

                Mouse.OverrideCursor =
                    Cursors.Wait;

                IsEnabled =
                    false;

                // SazToHarConverter.cs currently uses
                // namespace HarAnalyzerV2.
                int sessionCount =
                    await Task.Run(
                        () =>
                            HarAnalyzerV2
                                .SazToHarConverter
                                .Convert(
                                    openDialog.FileName,
                                    saveDialog.FileName));

                StatusText.Text =
                    $"Converted {sessionCount:N0} sessions.";

                MessageBoxResult result =
                    MessageBox.Show(
                        "SAZ conversion completed successfully!\n\n" +
                        $"Sessions converted: {sessionCount:N0}\n\n" +
                        "HAR file:\n" +
                        saveDialog.FileName +
                        "\n\n" +
                        "Do you want to load the generated HAR now?",
                        "SAZ → HAR",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                if (result ==
                    MessageBoxResult.Yes)
                {
                    LoadHarFile(
                        saveDialog.FileName);
                }
            }
            catch (InvalidDataException ex)
            {
                StatusText.Text =
                    "SAZ conversion failed.";

                MessageBox.Show(
                    ex.Message,
                    "Invalid SAZ file",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "SAZ conversion failed.";

                MessageBox.Show(
                    ex.ToString(),
                    "SAZ → HAR Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsEnabled =
                    true;

                Mouse.OverrideCursor =
                    null;
            }
        }

        // ============================================================
        // ZIP HAR
        // ============================================================

        private void ZipHar_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(
                    currentHarFile) ||

                !File.Exists(
                    currentHarFile))
            {
                MessageBox.Show(
                    "Please open a HAR file first.",
                    "HAR Analyzer V3",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            try
            {
                string directory =
                    IOPath.GetDirectoryName(
                        currentHarFile)
                    ??
                    Environment.CurrentDirectory;

                string zipFile =
                    IOPath.Combine(
                        directory,

                        IOPath.GetFileNameWithoutExtension(
                            currentHarFile) +
                        ".zip");

                if (File.Exists(
                        zipFile))
                {
                    MessageBoxResult result =
                        MessageBox.Show(
                            "The ZIP file already exists:\n\n" +
                            zipFile +
                            "\n\nDo you want to replace it?",
                            "HAR Analyzer V3",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                    if (result !=
                        MessageBoxResult.Yes)
                    {
                        return;
                    }

                    File.Delete(
                        zipFile);
                }

                using (
                    ZipArchive archive =
                        ZipFile.Open(
                            zipFile,
                            ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(
                        currentHarFile,
                        IOPath.GetFileName(
                            currentHarFile),
                        CompressionLevel.Optimal);
                }

                FileInfo original =
                    new(
                        currentHarFile);

                FileInfo compressed =
                    new(
                        zipFile);

                double reduction =
                    0;

                if (original.Length > 0)
                {
                    reduction =
                        100.0 -
                        (
                            (double)compressed.Length /
                            original.Length *
                            100.0
                        );
                }

                StatusText.Text =
                    "HAR compressed successfully.";

                MessageBox.Show(
                    "HAR file compressed successfully!\n\n" +
                    $"Original size: {FormatBytes(original.Length)}\n" +
                    $"ZIP size: {FormatBytes(compressed.Length)}\n" +
                    $"Reduction: {reduction:0.0}%\n\n" +
                    "Saved to:\n" +
                    zipFile,
                    "HAR Analyzer V3",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusText.Text =
                    "ZIP operation failed.";

                MessageBox.Show(
                    ex.ToString(),
                    "ZIP Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ============================================================
        // ABOUT
        // ============================================================

        private void About_Click(
            object sender,
            RoutedEventArgs e)
        {
            MessageBox.Show(
                "HAR Analyzer V3\n\n" +
                "HAR Analysis\n" +
                "SAZ to HAR Converter\n" +
                "HAR to ZIP\n" +
                "Statistics and Timeline\n\n" +
                "By Andrei-Emilian Rachita\n" +
                "(C) 2026 PhoeNIXBird Networks\n" +
                "www.pbnet.ro",
                "About HAR Analyzer V3",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private static DateTimeOffset?
            ParseStartedDateTime(
                JsonElement entry)
        {
            string value =
                GetStringProperty(
                    entry,
                    "startedDateTime");

            if (DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTimeOffset result))
            {
                return result;
            }

            return null;
        }

        private static double GetTiming(
            JsonElement entry,
            string name)
        {
            if (!entry.TryGetProperty(
                    "timings",
                    out JsonElement timings))
            {
                return -1;
            }

            if (!timings.TryGetProperty(
                    name,
                    out JsonElement value))
            {
                return -1;
            }

            if (value.ValueKind ==
                    JsonValueKind.Number &&

                value.TryGetDouble(
                    out double result))
            {
                return result;
            }

            return -1;
        }

        private static double Positive(
            double value)
        {
            return
                value < 0
                    ? 0
                    : value;
        }

        private static DateTimeOffset? AddMs(
            DateTimeOffset? value,
            double milliseconds)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return
                value.Value.AddMilliseconds(
                    milliseconds);
        }

        private static string FormatElapsed(
            double milliseconds)
        {
            if (milliseconds < 0)
            {
                return "N/A";
            }

            TimeSpan ts =
                TimeSpan.FromMilliseconds(
                    milliseconds);

            return
                $"{(int)ts.TotalHours}:" +
                $"{ts.Minutes:00}:" +
                $"{ts.Seconds:00}." +
                $"{ts.Milliseconds:000}";
        }

        private static void AppendHeaders(
            StringBuilder output,
            JsonElement parent)
        {
            if (!parent.TryGetProperty(
                    "headers",
                    out JsonElement headers) ||

                headers.ValueKind !=
                    JsonValueKind.Array)
            {
                return;
            }

            foreach (
                JsonElement header
                in headers.EnumerateArray())
            {
                output.AppendLine(
                    GetStringProperty(
                        header,
                        "name") +
                    ": " +
                    GetStringProperty(
                        header,
                        "value"));
            }
        }

        private static string GetStringProperty(
            JsonElement element,
            string propertyName)
        {
            if (element.TryGetProperty(
                    propertyName,
                    out JsonElement value) &&

                value.ValueKind ==
                    JsonValueKind.String)
            {
                return
                    value.GetString()
                    ?? "";
            }

            return "";
        }

        private static int GetIntProperty(
            JsonElement element,
            string propertyName)
        {
            if (element.TryGetProperty(
                    propertyName,
                    out JsonElement value) &&

                value.ValueKind ==
                    JsonValueKind.Number &&

                value.TryGetInt32(
                    out int result))
            {
                return result;
            }

            return 0;
        }

        private static long GetLongProperty(
            JsonElement element,
            string propertyName)
        {
            if (element.TryGetProperty(
                    propertyName,
                    out JsonElement value) &&

                value.ValueKind ==
                    JsonValueKind.Number &&

                value.TryGetInt64(
                    out long result))
            {
                return result;
            }

            return 0;
        }

        private static double GetDoubleProperty(
            JsonElement element,
            string propertyName)
        {
            if (element.TryGetProperty(
                    propertyName,
                    out JsonElement value) &&

                value.ValueKind ==
                    JsonValueKind.Number &&

                value.TryGetDouble(
                    out double result))
            {
                return result;
            }

            return 0;
        }

        private static string PrettyPrintJson(
            string text)
        {
            if (string.IsNullOrWhiteSpace(
                    text))
            {
                return text;
            }

            string trimmed =
                text.TrimStart();

            if (!trimmed.StartsWith(
                    "{") &&

                !trimmed.StartsWith(
                    "["))
            {
                return text;
            }

            try
            {
                using JsonDocument document =
                    JsonDocument.Parse(
                        text);

                return
                    JsonSerializer.Serialize(
                        document.RootElement,

                        new JsonSerializerOptions
                        {
                            WriteIndented =
                                true
                        });
            }
            catch
            {
                return text;
            }
        }

        private static bool LooksLikeText(
            byte[] data)
        {
            if (data.Length == 0)
            {
                return true;
            }

            int sampleLength =
                Math.Min(
                    data.Length,
                    4096);

            int controlCharacters =
                0;

            for (
                int i = 0;
                i < sampleLength;
                i++)
            {
                byte value =
                    data[i];

                if (value == 0)
                {
                    return false;
                }

                if (
                    value < 9 ||
                    (
                        value > 13 &&
                        value < 32
                    ))
                {
                    controlCharacters++;
                }
            }

            return
                controlCharacters <
                sampleLength *
                0.05;
        }

        private static string FormatBytes(
            long bytes)
        {
            if (bytes < 0)
            {
                return "N/A";
            }

            if (bytes < 1024)
            {
                return
                    $"{bytes:N0} bytes";
            }

            if (bytes <
                1024L * 1024L)
            {
                return
                    $"{bytes / 1024.0:N1} KB";
            }

            if (bytes <
                1024L *
                1024L *
                1024L)
            {
                return
                    $"{bytes / 1024.0 / 1024.0:N2} MB";
            }

            return
                $"{bytes / 1024.0 / 1024.0 / 1024.0:N2} GB";
        }
    }

    // ================================================================
    // HAR GRID MODEL
    // ================================================================

    public class HarItem
    {
        public int FrameNumber
        {
            get;
            set;
        }

        public string Method
        {
            get;
            set;
        } = "";

        public int Status
        {
            get;
            set;
        }

        public string Protocol
        {
            get;
            set;
        } = "";

        public string Host
        {
            get;
            set;
        } = "";

        public string Url
        {
            get;
            set;
        } = "";

        public string ContentType
        {
            get;
            set;
        } = "";

        public long BodySizeValue
        {
            get;
            set;
        }

        public double TimeValue
        {
            get;
            set;
        }

        public DateTimeOffset? StartedDateTime
        {
            get;
            set;
        }

        public JsonElement Entry
        {
            get;
            set;
        }

        public string BodySize
        {
            get
            {
                if (BodySizeValue < 1024)
                {
                    return
                        $"{BodySizeValue:N0} B";
                }

                if (BodySizeValue <
                    1024 * 1024)
                {
                    return
                        $"{BodySizeValue / 1024.0:N1} KB";
                }

                return
                    $"{BodySizeValue / 1024.0 / 1024.0:N2} MB";
            }
        }

        public string Time
        {
            get
            {
                if (TimeValue >= 1000)
                {
                    return
                        $"{TimeValue / 1000.0:0.00} s";
                }

                return
                    $"{TimeValue:0} ms";
            }
        }
    }
}
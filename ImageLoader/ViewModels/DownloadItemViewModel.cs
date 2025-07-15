using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageLoader.Infrastructure;

namespace ImageLoader.ViewModels;

/// <summary>
/// ViewModel одного слота загрузки изображения.
/// Хранит URL, прогресс, картинку и команды Старт/Стоп.
/// </summary>
public class DownloadItemViewModel : ObservableObject
{
    // HttpClient рекомендуется переиспользовать во всём приложении
    private static readonly HttpClient Http = new();

    private string _url = string.Empty;
    private double _progress;
    private ImageSource? _image;
    private bool _isDownloading;
    private CancellationTokenSource? _cts; // токен для отмены

    public string Url
    {
        get => _url;
        set
        {
            if (Set(ref _url, value))
                CommandManager.InvalidateRequerySuggested(); // пересчитать CanExecute
        }
    }

    /// <summary>Прогресс конкретного слота (0-100).</summary>
    public double Progress
    {
        get => _progress;
        private set => Set(ref _progress, value);
    }

    public ImageSource? ImageSource
    {
        get => _image;
        private set => Set(ref _image, value);
    }

    public bool IsDownloading
    {
        get => _isDownloading;
        private set
        {
            if (Set(ref _isDownloading, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }

    public DownloadItemViewModel()
    {
        StartCommand = new RelayCommand(
            async _ => await StartAsync(),
            _ => !IsDownloading && Uri.IsWellFormedUriString(Url, UriKind.Absolute));

        StopCommand = new RelayCommand(_ => Cancel(), _ => IsDownloading);
    }

    /// <summary>Запускает асинхронную загрузку картинки.</summary>
    public async Task StartAsync()
    {
        if (IsDownloading) return; // двойной клик
        if (!Uri.IsWellFormedUriString(Url, UriKind.Absolute)) return; // некорректный ввод

        IsDownloading = true;
        Progress = 0;

        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        // Прогресс пробрасываем через IProgress<T>, чтобы не думать о потоках
        // Progress<T>.Report всегда выполняется в UI-потоке
        var progress = new Progress<double>(p => Progress = p);

        try
        {
            // Скачиваем файл в память.
            var ms = await DownloadAsync(Url, progress, _cts.Token);

            // Создание BitmapImage может быть дорогим,
            // поэтому делаем это в Task.Run
            var img = await Task.Run(() =>
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad; // сразу читаем всё
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze(); // делаем потокобезопасным

                return (ImageSource)bmp;
            });

            ImageSource = img;
        }
        catch (OperationCanceledException)
        {
            // Пользователь нажал Стоп - не ошибка.
        }
        catch (Exception ex)
        {
            // В реальном приложении здесь логирование и уведомление пользователя.
            Console.WriteLine(ex);
        }
        finally
        {
            IsDownloading = false;
            Progress = 0;
        }
    }

    /// <summary>Отмена текущей загрузки.</summary>
    public void Cancel() => _cts?.Cancel();

    /// <summary>
    /// Скачивает файл из сети с поддержкой отмены и прогресса.
    /// Работает поблочно, чтобы не держать в памяти гигантский буфер и не блокировать поток UI.
    /// </summary>
    private static async Task<Stream> DownloadAsync(
        string url, IProgress<double> progress, CancellationToken token)
    {
        // HttpCompletionOption.ResponseHeadersRead - получаем поток сразу
        // после заголовков, не дожидаясь полной загрузки в буфер
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        var ms = new MemoryStream();

        var buffer = new byte[81920]; // стандартный размер буфера (80 КБ)
        long read = 0;

        while (true)
        {
            // Читаем кусок
            var len = await stream.ReadAsync(buffer, token);
            if (len == 0) break; // EOF

            // Пишем кусок
            await ms.WriteAsync(buffer.AsMemory(0, len), token);
            read += len;

            // Если сервер прислал Content-Length - считаем %
            if (total is not null)
                progress.Report(read * 100d / total.Value);
        }

        // Чтобы общий прогресс не зависал на 99,9
        progress.Report(100);
        ms.Position = 0;
        return ms;
    }
}
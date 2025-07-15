using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using ImageLoader.Infrastructure;

namespace ImageLoader.ViewModels;

/// <summary>Главная VM - хранит 3 слота и агрегирует общий прогресс.</summary>
public class MainViewModel : ObservableObject
{
    public ObservableCollection<DownloadItemViewModel> Items { get; } =
        new() { new(), new(), new() };

    public ICommand LoadAllCommand { get; }

    public ICommand AddNewDownloadItem { get; }

    public MainViewModel()
    {
        // Подписываемся на изменения, чтобы обновлять общий прогресс
        foreach (var item in Items)
            item.PropertyChanged += OnItemPropertyChanged;

        // Команда - Загрузить все
        LoadAllCommand = new RelayCommand(
            o =>
            {
                // Запускаем все три загрузки параллельно
                foreach (var item in Items)
                    _ = item.StartAsync(); // асинхронный вызов внутри самой VM
            },
            o => Items.All(item => item.StartCommand.CanExecute(null)));

        AddNewDownloadItem = new RelayCommand(o =>
            {
                var newItem = new DownloadItemViewModel();
                Items.Add(newItem);
                newItem.PropertyChanged += OnItemPropertyChanged;
            },
            o => true);
    }

    /// <summary>
    /// Среднее значение Progress всех активных слотов (0-100).
    /// </summary>
    public double TotalProgress
    {
        get
        {
            var downloadingItems = Items.Where(i => i.IsDownloading).ToList();
            if (downloadingItems.Count > 0)
            {
                return downloadingItems.Average(i => i.Progress);
            }

            return 0; // Возвращаем 0, если нет загружаемых элементов
        }
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Если изменился прогресс или состояние загрузки - пересчитываем общий прогресс
        if (e.PropertyName is nameof(DownloadItemViewModel.Progress) or
            nameof(DownloadItemViewModel.IsDownloading))
            OnPropertyChanged(nameof(TotalProgress));
    }
}
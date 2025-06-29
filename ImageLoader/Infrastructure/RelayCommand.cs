using System;
using System.Windows.Input;

namespace ImageLoader.Infrastructure;

/// <summary>
/// Реализация ICommand без внешних зависимостей.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>Может ли команда выполниться.</summary>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <summary>Само действие команды.</summary>
    public void Execute(object? parameter) => _execute(parameter);

    /// <summary>
    /// Стандартный паттерн WPF: CommandManager следит за состоянием ввода
    /// и пересчитывает доступность команд.
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
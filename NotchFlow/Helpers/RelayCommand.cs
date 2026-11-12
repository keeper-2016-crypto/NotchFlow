using System;
using System.Windows.Input;
namespace NotchFlow.Helpers;
public sealed class RelayCommand(Action action) : ICommand
{
 public bool CanExecute(object? parameter)=>true;
 public void Execute(object? parameter)=>action();
 public event EventHandler? CanExecuteChanged { add {} remove {} }
}

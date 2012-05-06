using System;
using System.Windows.Input;

namespace SimpleChat
{
	internal class DelegatedCommand<T>
		: DelegatedCommand
	{
		public DelegatedCommand (Action<T> execute)
			: base (s => execute ((T)s))
		{
			if (execute == null)
				throw new ArgumentNullException ("execute");
		}

		public DelegatedCommand (Action<T> execute, Func<T, bool> canExecute)
			: base (s => execute ((T)s), s => canExecute ((T)s))
		{
			if (execute == null)
				throw new ArgumentNullException ("execute");
			if (canExecute == null)
				throw new ArgumentNullException ("canExecute");
		}
	}

	internal class DelegatedCommand
		: ICommand
	{
		public DelegatedCommand (Action<object> execute)
			: this (execute, s => true)
		{
		}

		public DelegatedCommand (Action<object> execute, Func<object, bool> canExecute)
		{
			if (execute == null)
				throw new ArgumentNullException ("execute");
			if (canExecute == null)
				throw new ArgumentNullException ("canExecute");

			this.execute = execute;
			this.canExecute = canExecute;
		}

		public event EventHandler CanExecuteChanged;

		public void Execute (object parameter)
		{
			this.execute (parameter);
		}

		public bool CanExecute (object parameter)
		{
			return this.canExecute (parameter);
		}

		public void NotifyExecutabilityChanged()
		{
			var changed = CanExecuteChanged;
			if (changed != null)
				changed (this, EventArgs.Empty);
		}

		private readonly Action<object> execute;
		private readonly Func<object, bool> canExecute;
	}
}
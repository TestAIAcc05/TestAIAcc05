using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using Desktop.ImportTool.Infrastructure;
using Desktop.ImportTool.Models;
using Desktop.ImportTool.Views;

namespace Desktop.ImportTool.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly string dbFilePath = "C:\\ProgramData\\Cinegy\\Cinegy Convert\\CinegyImportTool.db";
        private readonly DBManager manager;
        private int _addTaskCounter = 1;

        public TasksViewModel TasksVM { get; }
        public HistoryViewModel HistoryVM { get; }
        public object TasksView { get; }
        public object HistoryView { get; }
        public ICommand FinishTaskCommand { get; }
        public ICommand FailTaskCommand { get; }
        public ICommand AddTaskCommand { get; }

        private bool _isTasksSelected = true;
        public bool IsTasksSelected
        {
            get => _isTasksSelected;
            set { _isTasksSelected = value; OnPropertyChanged(nameof(IsTasksSelected)); }
        }

        private bool _isHistorySelected = false;
        public bool IsHistorySelected
        {
            get => _isHistorySelected;
            set { _isHistorySelected = value; OnPropertyChanged(nameof(IsHistorySelected)); }
        }

        // VM-level commands to request panes (MVVM)
        public ICommand OpenTasksPaneCommand { get; }
        public ICommand OpenHistoryPaneCommand { get; }

        private readonly IDockingService _dockingService;

        public MainWindowViewModel() : this(null) { }

        // Preferred ctor: inject docking service (provided by View)
        public MainWindowViewModel(IDockingService dockingService)
        {
            _dockingService = dockingService;

            manager = new DBManager(dbFilePath);
            TasksVM = new TasksViewModel();
            HistoryVM = new HistoryViewModel();

            // VM creates view instances and sets their DataContext
            TasksView = new TasksView { DataContext = TasksVM };
            HistoryView = new HistoryView { DataContext = HistoryVM };

            FinishTaskCommand = new RelayCommand(_ => FinishTask());
            FailTaskCommand = new RelayCommand(_ => FailTask());
            AddTaskCommand = new RelayCommand(_ => AddTask());

            // Open commands: CanExecute returns false when pane already open.
            OpenTasksPaneCommand = new RelayCommand(_ => _dockingService?.OpenPane("Tasks"),
                                                    _ => !(_dockingService?.IsPaneOpen("Tasks") ?? false));

            OpenHistoryPaneCommand = new RelayCommand(_ => _dockingService?.OpenPane("History"),
                                                      _ => !(_dockingService?.IsPaneOpen("History") ?? false));

            // Subscribe to PaneStateChanged to update command enabled state when panes open/close.
            if (_dockingService != null)
            {
                _dockingService.PaneStateChanged += (s, e) =>
                {
                    // When pane state changes, refresh the commands' CanExecute.
                    // RelayCommand has RaiseCanExecuteChanged used elsewhere in repo.
                    (OpenTasksPaneCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (OpenHistoryPaneCommand as RelayCommand)?.RaiseCanExecuteChanged();
                };
            }
        }
        private void FinishTask()
        {

            try
            {
                var currentUser = Environment.UserName;
                var tasks = manager.GetTasks()
                                   .Where(t => t.CreatedBy == currentUser && t.Status != TaskStatus.Paused)
                                   .OrderBy(t => t.TaskOrder)
                                   .ToList();

                var taskToFinish = tasks.FirstOrDefault();
                if (taskToFinish == null) return;

                taskToFinish.Status = TaskStatus.Finished;
                taskToFinish.FinishingTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                manager.AddHistoryRow(taskToFinish);
                manager.DeleteTask(taskToFinish.Id);

                TasksVM.LoadTasks();
                HistoryVM.LoadHistory();
            }
            catch (Exception)
            {
                //TODO after logger will be added
            }
        }

        private void FailTask()
        {
            try
            {
                var currentUser = Environment.UserName;
                var tasks = manager.GetTasks()
                                   .Where(t => t.CreatedBy == currentUser && t.Status != TaskStatus.Paused)
                                   .OrderBy(t => t.TaskOrder)
                                   .ToList();

                var taskToFail = tasks.FirstOrDefault();
                if (taskToFail == null) return;

                taskToFail.Status = TaskStatus.Failed;
                taskToFail.FinishingTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                manager.AddHistoryRow(taskToFail);
                manager.DeleteTask(taskToFail.Id);

                TasksVM.LoadTasks();
                HistoryVM.LoadHistory();
            }
            catch (Exception)
            {
                //TODO after logger will be added
            }
        }
        private void AddTask()
        {
            try
            {
                var currentUser = Environment.UserName;
                manager.AddTask("Source" + _addTaskCounter, "Target" + _addTaskCounter, "Metadata" + _addTaskCounter, "Settings" + _addTaskCounter);
                _addTaskCounter++;
                TasksVM.LoadTasks();
            }
            catch (Exception)
            {
                //TODO after logger will be added
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Desktop.ImportTool.Models;
using Desktop.ImportTool.Infrastructure;
using System.Collections.Specialized;

namespace Desktop.ImportTool.ViewModels
{
    public class HistoryViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<TaskModel> History { get; set; }
        private ObservableCollection<TaskModel> _selectedTasks = new ObservableCollection<TaskModel>();
        public ObservableCollection<TaskModel> SelectedTasks
        {
            get => _selectedTasks;
            set
            {
                if (_selectedTasks != value)
                {
                    if (_selectedTasks != null)
                        _selectedTasks.CollectionChanged -= SelectedTasks_CollectionChanged;

                    _selectedTasks = value ?? new ObservableCollection<TaskModel>();
                    _selectedTasks.CollectionChanged += SelectedTasks_CollectionChanged;

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedTasks)));
                }
            }
        }

        private void SelectedTasks_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            DeleteSelectedHistoryCommand?.RaiseCanExecuteChanged();
        }

        public RelayCommand DeleteSelectedHistoryCommand { get; private set; }
        public RelayCommand DeleteHistoryRowCommand { get; }

        private readonly string dbFilePath = "C:\\ProgramData\\Cinegy\\Cinegy Convert\\CinegyImportTool.db";

        public HistoryViewModel()
        {
            History = new ObservableCollection<TaskModel>();
            SelectedTasks = new ObservableCollection<TaskModel>();
            LoadHistory();
            DeleteSelectedHistoryCommand = new RelayCommand(_ => DeleteSelectedHistory(),
              _ =>
              {
                  return SelectedTasks != null && SelectedTasks.Count > 0;
              });
            DeleteHistoryRowCommand = new RelayCommand(task => DeleteHistoryTask(task as TaskModel));
        }

        public void LoadHistory()
        {
            var manager = new DBManager(dbFilePath);
            var allHistory = manager.GetHistory();
            History.Clear();
            foreach (var item in allHistory)
                History.Add(item);
        }

        private void DeleteSelectedHistory()
        {
            var manager = new DBManager(dbFilePath);
            var toDelete = SelectedTasks.ToList();
            foreach (var task in toDelete)
            {
                manager.DeleteHistoryRow(task.Id);
                History.Remove(task);
            }
        }

        private void DeleteHistoryTask(TaskModel task)
        {
            if (task == null) return;
            var manager = new DBManager(dbFilePath);
            manager.DeleteHistoryRow(task.Id);
            History.Remove(task);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}

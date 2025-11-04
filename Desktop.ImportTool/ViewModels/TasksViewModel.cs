using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using Desktop.ImportTool.Infrastructure;
using Desktop.ImportTool.Models;


namespace Desktop.ImportTool.ViewModels
{
    public class TasksViewModel : INotifyPropertyChanged
    {
        #region Members
        public ObservableCollection<TaskModel> Tasks { get; } = new ObservableCollection<TaskModel>();
        public ICollectionView TasksView { get; }
        public string CurrentUser { get; } = Environment.UserName;

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

        private readonly string dbFilePath = "C:\\ProgramData\\Cinegy\\Cinegy Convert\\CinegyImportTool.db";
        private TaskModel _lastStartTask = null;
        #endregion

        #region Commands
        public RelayCommand RunTaskCommand { get; }
        public RelayCommand RunSelectedTasksCommand { get; }
        public RelayCommand PauseTaskCommand { get; }
        public RelayCommand PauseSelectedTasksCommand { get; }
        public RelayCommand DeleteTaskCommand { get; }
        public RelayCommand DeleteSelectedTasksCommand { get; private set; }
        public RelayCommand MoveUpCommand { get; }
        public RelayCommand MoveDownCommand { get; }
        public RelayCommand MoveTasksToStartCommand { get; }
        public RelayCommand MoveTasksToFinishCommand { get; }
        #endregion

        #region Constructors
        public TasksViewModel()
        {
            Tasks = new ObservableCollection<TaskModel>();
            SelectedTasks = new ObservableCollection<TaskModel>();
            RunTaskCommand = new RelayCommand(
                param => RunTask(param as TaskModel));
            RunSelectedTasksCommand = new RelayCommand(
                param => RunSelectedTasks(),
                param => SelectedTasks?.Any(t => t.Status != TaskStatus.Queued) == true
            );
            PauseTaskCommand = new RelayCommand(
                param => PauseTask(param as TaskModel));
            PauseSelectedTasksCommand = new RelayCommand(
                param => PauseSelectedTasks(),
                param => SelectedTasks?.Any(t => t.Status != TaskStatus.Paused) == true
            );
            DeleteTaskCommand = new RelayCommand(param => DeleteTask(param as TaskModel));
            DeleteSelectedTasksCommand = new RelayCommand(_ => DeleteSelectedTasks(),
                                                          _ => SelectedTasks != null && SelectedTasks.Count > 0);
            MoveUpCommand = new RelayCommand(_ => MoveTasksUp(), _ => CanMoveTasksUp());
            MoveDownCommand = new RelayCommand(_ => MoveTasksDown(), _ => CanMoveTasksDown());
            MoveTasksToStartCommand = new RelayCommand(_ => MoveTasksToStart(), _ => CanMoveTasksToStart());
            MoveTasksToFinishCommand = new RelayCommand(_ => MoveTasksToFinish(), _ => CanMoveTasksDown());

            SelectedTasks.CollectionChanged += SelectedTasks_CollectionChanged;
            foreach (var task in Tasks)
                task.PropertyChanged += Task_PropertyChanged;

            var manager = new DBManager(dbFilePath);
            foreach (var task in manager.GetTasks())
                Tasks.Add(task);
            TasksView = CollectionViewSource.GetDefaultView(Tasks);
            TasksView.Filter = o =>
            {
                var task = o as TaskModel;
                return task != null && task.CreatedBy == CurrentUser;
            };
        }
        #endregion

        #region PropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Event Handlers
        private void SelectedTasks_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            DeleteSelectedTasksCommand?.RaiseCanExecuteChanged();
            MoveUpCommand?.RaiseCanExecuteChanged();
            MoveDownCommand?.RaiseCanExecuteChanged();
            MoveTasksToStartCommand?.RaiseCanExecuteChanged();
            MoveTasksToFinishCommand?.RaiseCanExecuteChanged();
            RunSelectedTasksCommand?.RaiseCanExecuteChanged();
            PauseTaskCommand?.RaiseCanExecuteChanged();
            PauseSelectedTasksCommand?.RaiseCanExecuteChanged();

            if (e.NewItems != null)
            {
                foreach (TaskModel task in e.NewItems)
                {
                    task.PropertyChanged += SelectedTask_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (TaskModel task in e.OldItems)
                {
                    task.PropertyChanged -= SelectedTask_PropertyChanged;
                }
            }
        }

        private void SelectedTask_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TaskModel.Status))
            {
                PauseSelectedTasksCommand?.RaiseCanExecuteChanged();
                RunSelectedTasksCommand?.RaiseCanExecuteChanged();
            }
        }
        private void Task_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TaskModel.Status))
            {
                MoveTasksToStartCommand?.RaiseCanExecuteChanged();
            }
        }
        #endregion

        #region Methods
        public void DeleteTask(TaskModel task)
        {
            if (task == null) return;
            var manager = new DBManager(dbFilePath);
            manager.DeleteTask(task.Id);
            Tasks.Remove(task);

            UpdateTaskOrders();

            if (Tasks.Count > 0 && Tasks[0].Status == TaskStatus.Paused)
            {
                var nextQueued = Tasks.FirstOrDefault(t => t.Status == TaskStatus.Queued);
                if (nextQueued != null && Tasks.IndexOf(nextQueued) > 0)
                {
                    Tasks.Move(Tasks.IndexOf(nextQueued), 0);
                }
            }
        }

        private void DeleteSelectedTasks()
        {
            var manager = new DBManager(dbFilePath);
            var tasksToDelete = SelectedTasks.Cast<TaskModel>().ToList();

            foreach (var task in tasksToDelete)
            {
                manager.DeleteTask(task.Id);
                Tasks.Remove(task);
            }
            UpdateTaskOrders();

            if (Tasks.Count > 0 && Tasks[0].Status == TaskStatus.Paused)
            {
                var nextQueued = Tasks.FirstOrDefault(t => t.Status == TaskStatus.Queued);
                if (nextQueued != null && Tasks.IndexOf(nextQueued) > 0)
                {
                    Tasks.Move(Tasks.IndexOf(nextQueued), 0);
                }
            }
        }

        private void MoveTasksUp()
        {
            if (SelectedTasks == null || SelectedTasks.Count == 0) return;

            var selectedIds = SelectedTasks.Select(t => t.Id).ToList();

            var all = Tasks.ToList();

            var byUser = SelectedTasks.GroupBy(t => t.CreatedBy).ToList();

            foreach (var group in byUser)
            {
                string createdBy = group.Key;
                var userTasks = all.Select((task, idx) => (task, idx))
                    .Where(t => t.task.CreatedBy == createdBy)
                    .ToList();

                var selectedIndices = group
                    .Select(t => userTasks.FindIndex(u => u.task == t))
                    .Where(i => i >= 0)
                    .OrderBy(i => i)
                    .ToList();

                var blocks = new List<(int start, int end)>();
                int? blockStart = null;
                for (int i = 0; i < selectedIndices.Count; i++)
                {
                    if (blockStart == null)
                        blockStart = selectedIndices[i];
                    if (i == selectedIndices.Count - 1 || selectedIndices[i + 1] != selectedIndices[i] + 1)
                    {
                        blocks.Add((blockStart.Value, selectedIndices[i]));
                        blockStart = null;
                    }
                }

                bool anyNonPaused = all.Any(t => t.Status != TaskStatus.Paused);

                for (int b = blocks.Count - 1; b >= 0; b--)
                {
                    var (start, end) = blocks[b];
                    int blockSize = end - start + 1;
                    if (start == 0) continue;

                    int aboveIdx = userTasks[start - 1].idx;
                    var blockIndices = userTasks.GetRange(start, blockSize).Select(u => u.idx).ToList();
                    var block = blockIndices.Select(idx => all[idx]).ToList();

                    foreach (var idx in blockIndices.OrderByDescending(x => x))
                        all.RemoveAt(idx);

                    bool blockIsPaused = block.All(t => t.Status == TaskStatus.Paused);

                    int insertAt = aboveIdx;
                    if (blockIsPaused && anyNonPaused && aboveIdx == 0)
                        insertAt = 1;

                    foreach (var task in block)
                        all.Insert(insertAt++, task);
                }
            }

            for (int i = 0; i < all.Count; i++)
            {
                var expected = all[i];
                var currentIdx = Tasks.IndexOf(expected);
                if (currentIdx != i)
                    Tasks.Move(currentIdx, i);
            }

            UpdateTaskOrders();

            SelectedTasks.Clear();
            foreach (var task in Tasks.Where(t => selectedIds.Contains(t.Id)))
                SelectedTasks.Add(task);
        }

        private void MoveTasksDown()
        {
            if (SelectedTasks == null || SelectedTasks.Count == 0)
                return;

            var selectedIds = SelectedTasks.Select(t => t.Id).ToList();

            var all = Tasks.ToList();

            var byUser = SelectedTasks
                .GroupBy(t => t.CreatedBy)
                .ToList();

            foreach (var group in byUser)
            {
                string createdBy = group.Key;
                var userTasks = all
                    .Select((task, idx) => (task, idx))
                    .Where(t => t.task.CreatedBy == createdBy)
                    .ToList();

                var selectedIndices = group
                    .Select(t => userTasks.FindIndex(u => u.task == t))
                    .Where(i => i >= 0)
                    .OrderBy(i => i)
                    .ToList();

                var blocks = new List<(int start, int end)>();
                int? blockStart = null;
                for (int i = 0; i < selectedIndices.Count; i++)
                {
                    if (blockStart == null)
                        blockStart = selectedIndices[i];
                    if (i == selectedIndices.Count - 1 || selectedIndices[i + 1] != selectedIndices[i] + 1)
                    {
                        blocks.Add((blockStart.Value, selectedIndices[i]));
                        blockStart = null;
                    }
                }

                for (int b = blocks.Count - 1; b >= 0; b--)
                {
                    var (start, end) = blocks[b];
                    int blockSize = end - start + 1;
                    if (end >= userTasks.Count - 1)
                        continue;

                    int belowIdx = userTasks[end + 1].idx;
                    var blockIndices = userTasks.GetRange(start, blockSize).Select(u => u.idx).ToList();

                    var block = blockIndices.Select(idx => all[idx]).ToList();
                    foreach (var idx in blockIndices.OrderByDescending(x => x))
                        all.RemoveAt(idx);

                    int updatedBelowIdx = all.IndexOf(userTasks[end + 1].task);

                    int insertAt = updatedBelowIdx + 1;
                    foreach (var task in block)
                        all.Insert(insertAt++, task);
                }
            }

            for (int i = 0; i < all.Count; i++)
            {
                var expected = all[i];
                var currentIdx = Tasks.IndexOf(expected);
                if (currentIdx != i)
                    Tasks.Move(currentIdx, i);
            }

            UpdateTaskOrders();

            SelectedTasks.Clear();
            foreach (var task in Tasks.Where(t => selectedIds.Contains(t.Id)))
                SelectedTasks.Add(task);
        }

        private void MoveTasksToStart()
        {
            if (SelectedTasks == null || SelectedTasks.Count == 0) return;

            var selectedIds = SelectedTasks.Select(t => t.Id).ToList();
            var all = Tasks.ToList();

            var byUser = SelectedTasks.GroupBy(t => t.CreatedBy).ToList();

            foreach (var group in byUser)
            {
                string createdBy = group.Key;
                var userTasks = all.Select((task, idx) => (task, idx))
                    .Where(t => t.task.CreatedBy == createdBy)
                    .ToList();

                var selectedOrdered = userTasks
                    .Where(t => selectedIds.Contains(t.task.Id))
                    .Select(t => t.task)
                    .ToList();

                bool allSelectedPaused = selectedOrdered.All(t => t.Status == TaskStatus.Paused);
                bool allTasksPaused = all.All(t => t.Status == TaskStatus.Paused);
                bool anyNonPaused = all.Any(t => t.Status != TaskStatus.Paused);

                var firstQueued = selectedOrdered.FirstOrDefault(t => t.Status == TaskStatus.Queued);

                List<TaskModel> orderedBlock;
                int insertAt = 0;
                if (allSelectedPaused)
                {
                    orderedBlock = selectedOrdered;
                    if (anyNonPaused)
                        insertAt = 1;
                    else
                        insertAt = 0;
                }
                else if (firstQueued != null)
                {
                    orderedBlock = new List<TaskModel> { firstQueued };
                    orderedBlock.AddRange(selectedOrdered.Where(t => t != firstQueued));
                    insertAt = 0;
                }
                else
                {
                    orderedBlock = selectedOrdered;
                    insertAt = 0;
                }

                foreach (var tsk in orderedBlock)
                    all.Remove(tsk);

                foreach (var tsk in orderedBlock)
                    all.Insert(insertAt++, tsk);
            }

            for (int i = 0; i < all.Count; i++)
            {
                var expected = all[i];
                var currentIdx = Tasks.IndexOf(expected);
                if (currentIdx != i)
                    Tasks.Move(currentIdx, i);
            }

            UpdateTaskOrders();

            SelectedTasks.Clear();
            foreach (var task in Tasks.Where(t => selectedIds.Contains(t.Id)))
                SelectedTasks.Add(task);
        }

        private void MoveTasksToFinish()
        {
            if (SelectedTasks == null || SelectedTasks.Count == 0) return;

            var selectedIds = SelectedTasks.Select(t => t.Id).ToList();

            var all = Tasks.ToList();

            var byUser = SelectedTasks
                .GroupBy(t => t.CreatedBy)
                .ToList();

            foreach (var group in byUser)
            {
                string createdBy = group.Key;
                var userTasks = all
                    .Select((task, idx) => (task, idx))
                    .Where(t => t.task.CreatedBy == createdBy)
                    .ToList();

                var selectedIndices = group
                    .Select(t => userTasks.FindIndex(u => u.task == t))
                    .Where(i => i >= 0)
                    .OrderBy(i => i)
                    .ToList();

                var blockIndices = selectedIndices.Select(i => userTasks[i].idx).ToList();
                var block = blockIndices.Select(idx => all[idx]).ToList();

                foreach (var idx in blockIndices.OrderByDescending(x => x))
                    all.RemoveAt(idx);

                int insertAt = all.FindLastIndex(t => t.CreatedBy == createdBy);
                insertAt = insertAt < 0 ? all.Count : insertAt + 1;

                foreach (var task in block)
                    all.Insert(insertAt++, task);
            }

            for (int i = 0; i < all.Count; i++)
            {
                var expected = all[i];
                var currentIdx = Tasks.IndexOf(expected);
                if (currentIdx != i)
                    Tasks.Move(currentIdx, i);
            }

            UpdateTaskOrders();

            SelectedTasks.Clear();
            foreach (var task in Tasks.Where(t => selectedIds.Contains(t.Id)))
                SelectedTasks.Add(task);
        }

        private void UpdateTaskOrders()
        {
            var groups = Tasks.GroupBy(t => t.CreatedBy);
            foreach (var group in groups)
            {
                int order = 1;
                foreach (var task in Tasks.Where(t => t.CreatedBy == group.Key))
                {
                    task.TaskOrder = order++;
                }
            }
            var manager = new DBManager(dbFilePath);
            manager.UpdateTaskOrders(Tasks);
        }
        private bool CanMoveTasksToStart()
        {
            if (SelectedTasks == null || SelectedTasks.Count == 0) return false;
            bool anyNonPaused = Tasks.Any(t => t.Status != TaskStatus.Paused);

            if (SelectedTasks.All(t => t.Status == TaskStatus.Paused) && anyNonPaused)
            {
                var pausedIndices = SelectedTasks.Select(t => Tasks.IndexOf(t)).OrderBy(i => i).ToList();
                for (int i = 0; i < pausedIndices.Count; i++)
                {
                    if (pausedIndices[i] != i + 1)
                        return true;
                }
                return false;
            }

            if (SelectedTasks.Count == 1)
            {
                var task = SelectedTasks.First();
                int idx = Tasks.IndexOf(task);
                if (task.Status == TaskStatus.Paused && anyNonPaused)
                    return idx != 1;
                else
                    return idx != 0;
            }
            return true;
        }
        private bool CanMoveTasksUp()
        {
            if (SelectedTasks == null || SelectedTasks.Count == 0) return false;

            var selectedGroups = SelectedTasks.GroupBy(t => t.CreatedBy);
            foreach (var group in selectedGroups)
            {
                var allInGroup = Tasks.Where(t => t.CreatedBy == group.Key).OrderBy(t => t.TaskOrder).ToList();
                var selectedInGroup = group.Select(t => allInGroup.IndexOf(t)).OrderBy(i => i).ToList();

                bool anyNonPaused = allInGroup.Any(t => t.Status != TaskStatus.Paused);

                foreach (var idx in selectedInGroup)
                {
                    var task = allInGroup[idx];

                    if (task.Status == TaskStatus.Paused && anyNonPaused)
                    {
                        if (idx > 1 && !selectedInGroup.Contains(idx - 1))
                            return true;
                    }
                    else
                    {
                        if (idx > 0 && !selectedInGroup.Contains(idx - 1))
                            return true;
                    }
                }
            }
            return false;
        }

        private bool CanMoveTasksDown()
        {
            if (SelectedTasks == null || SelectedTasks.Count == 0) return false;

            var selectedGroups = SelectedTasks.GroupBy(t => t.CreatedBy);
            foreach (var group in selectedGroups)
            {
                var allInGroup = Tasks.Where(t => t.CreatedBy == group.Key).OrderBy(t => t.TaskOrder).ToList();
                var selectedInGroup = group.Select(t => allInGroup.IndexOf(t)).OrderByDescending(i => i).ToList();

                if (selectedInGroup.Any(i => i < allInGroup.Count - 1 && !selectedInGroup.Contains(i + 1)))
                    return true;
            }
            return false;
        }

        public void LoadTasks()
        {
            var manager = new DBManager(dbFilePath);
            var loaded = manager.GetTasks();
            Tasks.Clear();
            foreach (var task in loaded.Where(t => t.CreatedBy == CurrentUser))
                Tasks.Add(task);
            EnsurePausedSecondUnlessAllPaused();
            UpdateTaskOrders();
        }

        private void EnsurePausedSecondUnlessAllPaused()
        {
            var nonPaused = Tasks.Where(t => t.Status != TaskStatus.Paused).ToList();
            var paused = Tasks.Where(t => t.Status == TaskStatus.Paused).ToList();

            if (nonPaused.Count == 0)
                return;

            var firstNonPaused = nonPaused.First();
            var firstPaused = paused.FirstOrDefault();
            var idxNonPaused = Tasks.IndexOf(firstNonPaused);
            if (idxNonPaused != 0)
                Tasks.Move(idxNonPaused, 0);

            if (firstPaused != null)
            {
                var idxPaused = Tasks.IndexOf(firstPaused);
                if (idxPaused != 1)
                    Tasks.Move(idxPaused, 1);
            }
        }

        private void RunTask(TaskModel task)
        {
            if (task == null || task.Status == TaskStatus.Queued)
                return;

            var selectedIds = SelectedTasks.Select(t => t.Id).ToList();

            bool allPausedBefore = Tasks.All(t => t.Status == TaskStatus.Paused);

            task.Status = TaskStatus.Queued;
            var manager = new DBManager(dbFilePath);
            manager.UpdateTaskStatus(task.Id, TaskStatus.Queued.ToString());

            if (allPausedBefore)
            {
                Tasks.Remove(task);
                Tasks.Insert(0, task);
            }

            SelectedTasks.Clear();
            foreach (var t in Tasks.Where(x => selectedIds.Contains(x.Id)))
                SelectedTasks.Add(t);

            OnPropertyChanged(nameof(Tasks));
        }

        private void RunSelectedTasks()
        {
            var selectedIds = SelectedTasks.Select(t => t.Id).ToList();
            bool allPausedBefore = Tasks.All(t => t.Status == TaskStatus.Paused);
            var tasksToRun = SelectedTasks.Where(t => t.Status == TaskStatus.Paused).ToList();
            var tasksToRunIndices = tasksToRun.Select(t => Tasks.IndexOf(t)).ToList();

            foreach (var task in tasksToRun)
            {
                task.Status = TaskStatus.Queued;
                var manager = new DBManager(dbFilePath);
                manager.UpdateTaskStatus(task.Id, TaskStatus.Queued.ToString());
            }

            if (allPausedBefore && tasksToRun.Count > 0)
            {
                int minIndex = tasksToRunIndices.Min();
                var firstQueued = Tasks[minIndex];

                if (Tasks.IndexOf(firstQueued) != 0)
                {
                    Tasks.Move(Tasks.IndexOf(firstQueued), 0);
                }
            }

            SelectedTasks.Clear();
            foreach (var t in Tasks.Where(x => selectedIds.Contains(x.Id)))
                SelectedTasks.Add(t);

            OnPropertyChanged(nameof(Tasks));
        }
        private void PauseTask(TaskModel task)
        {
            if (task == null || task.Status == TaskStatus.Paused)
                return;

            var selectedIds = SelectedTasks.Select(t => t.Id).ToList();
            bool wasFirstAndQueued = Tasks.Count > 1 && Tasks[0] == task && task.Status == TaskStatus.Queued;
            task.Status = TaskStatus.Paused;
            var manager = new DBManager(dbFilePath);
            manager.UpdateTaskStatus(task.Id, TaskStatus.Paused.ToString());

            if (wasFirstAndQueued)
            {
                var nextQueued = Tasks.FirstOrDefault(t => t.Status == TaskStatus.Queued);
                if (nextQueued != null && Tasks.IndexOf(nextQueued) > 0)
                {
                    Tasks.Move(Tasks.IndexOf(nextQueued), 0);
                }
            }

            SelectedTasks.Clear();
            foreach (var t in Tasks.Where(x => selectedIds.Contains(x.Id)))
                SelectedTasks.Add(t);

            OnPropertyChanged(nameof(Tasks));
        }

        private void PauseSelectedTasks()
        {
            var wasFirstQueued = Tasks.Count > 1 && Tasks[0].Status == TaskStatus.Queued && SelectedTasks.Contains(Tasks[0]);
            var selectedIds = SelectedTasks.Select(t => t.Id).ToList();

            foreach (var task in SelectedTasks.Where(t => t.Status != TaskStatus.Paused))
            {
                task.Status = TaskStatus.Paused;
                var manager = new DBManager(dbFilePath);
                manager.UpdateTaskStatus(task.Id, TaskStatus.Paused.ToString());
            }

            if (wasFirstQueued)
            {
                var nextQueued = Tasks.FirstOrDefault(t => t.Status == TaskStatus.Queued);
                if (nextQueued != null && Tasks.IndexOf(nextQueued) > 0)
                {
                    Tasks.Move(Tasks.IndexOf(nextQueued), 0);
                }
            }

            SelectedTasks.Clear();
            foreach (var t in Tasks.Where(x => selectedIds.Contains(x.Id)))
                SelectedTasks.Add(t);

            OnPropertyChanged(nameof(Tasks));
        }
        #endregion

        //TODO check if a task should be (re-)run for the current user
        private void CheckTaskStatus()
        {
            var user = CurrentUser;
            var userTasks = Tasks.Where(t => t.CreatedBy == user)
                                 .OrderBy(t => t.TaskOrder)
                                 .ToList();

            if (userTasks.Count == 0)
            {
                return;
            }

            var topTask = userTasks.First();

            if (_lastStartTask == null || _lastStartTask.Id != topTask.Id)
            {
                if (_lastStartTask != null)
                {
                    PauseTask(_lastStartTask);
                }
                StartTask(topTask);
                _lastStartTask = topTask;
            }
        }

        private void StartTask(TaskModel task)
        {
            // TODO implement actual call to RunAgent with task

        }
    }
}
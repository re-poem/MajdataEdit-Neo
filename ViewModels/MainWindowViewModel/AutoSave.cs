using MajdataEdit_Neo.Base;
using MajdataEdit_Neo.Modules.AutoSave;
using MajdataEdit_Neo.Modules.AutoSave.Contexts;
using MajSimai;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MajdataEdit_Neo.ViewModels;

/// <summary>
/// 自动保存管理
/// </summary>
public partial class MainWindowViewModel
{
    InternalAutoSaveContext _localContext = null!;
    InternalAutoSaveContext _globalContext = null!;
    readonly InternalAutoSaveContentProvider _contentProvider = new();
    AutoSaveManager _manager = null!;
    readonly Lock _syncLock = new();

    SimaiFile? _pendingSimaiFile;
    long _updateVersion;
    bool _updateWorkerRunning;
    Task _updateTask = Task.CompletedTask;

    public bool IsFileChanged
    {
        get => _manager.IsFileChanged;
        set => _manager.IsFileChanged = value;
    }

    public bool Enabled
    {
        get => _manager.Enabled;
        set => _manager.Enabled = value;
    }

    private void InitializeAutoSave()
    {
        _localContext = new InternalAutoSaveContext(_contentProvider);
        _globalContext = new InternalAutoSaveContext(_contentProvider)
        {
            WorkingPath = MajEnv.GlobalAutoSaveDir
        };
        AutoSaveManager.Initialize(_localContext, _globalContext);
        _manager = AutoSaveManager.Instance;
    }

    public void UpdateContext(string maidataDir)
    {
        _localContext.RawFilePath = Path.Combine(maidataDir, "maidata.txt");
        _localContext.WorkingPath = Path.Combine(maidataDir, ".autosave");
        _globalContext.RawFilePath = Path.Combine(maidataDir, "maidata.txt");
    }

    public IReadOnlyCollection<AutoSaveFileInfo> GetLocalAutoSaves()
    {
        return _manager.Recoverer.GetLocalAutoSaves();
    }

    public IReadOnlyCollection<AutoSaveFileInfo> GetGlobalAutoSaves()
    {
        return _manager.Recoverer.GetGlobalAutoSaves();
    }

    public bool RecoverFile(AutoSaveFileInfo autoSaveFile)
    {
        return _manager.Recoverer.RecoverFile(autoSaveFile);
    }

    public void SetContent(string content)
    {
        lock (_syncLock)
        {
            _pendingSimaiFile = null;
            _updateVersion++;
            _contentProvider.Content = content;
        }
    }

    public Task OnSimaiFileChangedAsync(SimaiFile? simaiFile)
    {
        var startWorker = false;
        lock (_syncLock)
        {
            _pendingSimaiFile = simaiFile;
            _updateVersion++;
            if (!_updateWorkerRunning)
            {
                _updateWorkerRunning = true;
                startWorker = true;
            }
        }

        if (startWorker)
            _updateTask = ProcessPendingUpdatesAsync();

        return _updateTask;
    }

    private async Task ProcessPendingUpdatesAsync()
    {
        while (true)
        {
            SimaiFile? pendingFile;
            long version;
            lock (_syncLock)
            {
                pendingFile = _pendingSimaiFile;
                version = _updateVersion;
            }

            if (pendingFile is null)
            {
                lock (_syncLock)
                    _updateWorkerRunning = false;
                return;
            }

            try
            {
                var maidata = await SimaiParser.DeparseAsync(pendingFile);

                lock (_syncLock)
                {
                    if (version != _updateVersion)
                        continue;

                    _contentProvider.Content = maidata;
                    _updateWorkerRunning = false;
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                lock (_syncLock)
                {
                    if (version != _updateVersion)
                        continue;

                    _updateWorkerRunning = false;
                    return;
                }
            }
        }
    }

    internal class InternalAutoSaveContext : IAutoSaveContext, IAutoSaveContentProvider<string>
    {
        public string WorkingPath { get; set; } = string.Empty;
        public string RawFilePath { get; set; } = string.Empty;
        public string Content => _contentProvider?.Content ?? string.Empty;

        readonly IAutoSaveContentProvider<string>? _contentProvider;

        public InternalAutoSaveContext(IAutoSaveContentProvider<string>? contentProvider)
        {
            _contentProvider = contentProvider;
        }
        public InternalAutoSaveContext()
        {
        }
    }

    internal class InternalAutoSaveContentProvider : IAutoSaveContentProvider<string>
    {
        public string Content { get; set; } = string.Empty;
    }
}

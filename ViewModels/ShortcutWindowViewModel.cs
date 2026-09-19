using MajdataEdit_Neo.Types;
using System.Collections.Generic;
using Types;

namespace MajdataEdit_Neo.ViewModels;

public class ShortcutWindowViewModel : ViewModelBase
{
    public IReadOnlyList<ShortcutDefinition> Shortcuts => ShortcutDefinitions.All;
}

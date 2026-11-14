"""Compile the foundation against the installed Unity 6 assemblies without starting Unity."""
import pathlib
import subprocess
import tempfile
root = pathlib.Path(__file__).resolve().parents[3]
scripting = pathlib.Path('/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/Resources/Scripting')
refs = list((scripting / 'UnityReferenceAssemblies/unity-4.8-api').glob('*.dll'))
refs += list((scripting / 'UnityReferenceAssemblies/unity-4.8-api/Facades').glob('*.dll'))
refs += list((scripting / 'Managed/UnityEngine').glob('*.dll'))
refs += [root / 'games/unity/Library/ScriptAssemblies' / name for name in ['UnityEngine.UI.dll', 'Unity.InputSystem.dll']]
sources = list((root / 'games/unity/Assets/_Game/UI/Immersive').rglob('*.cs'))
with tempfile.TemporaryDirectory(prefix='contextspeak-ui-compile-') as tmp:
    subprocess.run([str(scripting / 'NetCoreRuntime/dotnet'), str(scripting / 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-nostdlib+', '-target:library', '-out:' + tmp + '/ContextSpeakUI.dll', *['-r:' + str(p) for p in refs], *[str(p) for p in sources]], check=True)
    print('Foundation compiles against Unity 6000.3.28f1.')

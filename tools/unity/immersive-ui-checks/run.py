"""Run the actual portable C# UI policy, using Unity's bundled .NET (no Editor launch)."""
import json
import pathlib
import subprocess
import tempfile

root = pathlib.Path(__file__).resolve().parents[3]
scripting = pathlib.Path('/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/Resources/Scripting')
dotnet = scripting / 'NetCoreRuntime/dotnet'
refs = list((scripting / 'NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21').glob('*.dll'))
policy = root / 'games/unity/Assets/_Game/UI/Immersive/UiPolicy.cs'
with tempfile.TemporaryDirectory(prefix='contextspeak-ui-') as tmp:
    target = pathlib.Path(tmp) / 'UiChecks.dll'
    sources = [str(pathlib.Path(__file__).with_name('Program.cs'))]
    if policy.exists():
        sources.append(str(policy))
    subprocess.run([str(dotnet), str(scripting / 'DotNetSdkRoslyn/csc.dll'), '-nologo', '-target:exe', '-out:' + str(target), *['-r:' + str(p) for p in refs], *sources], check=True)
    target.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {'tfm': 'net6.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': '6.0.21'}}}))
    subprocess.run([str(dotnet), str(target)], check=True)

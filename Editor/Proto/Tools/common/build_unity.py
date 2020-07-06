from common import utility as u
from res import patchlize_resource
from res import patchlize_dlc
from res import append_resource_hash
from common import log_filter

unity_log = u.get_temp_path('unity_build' + u.get_env('JOB_NAME', '') + '.log')

def execute_unity(method, ext_args=None):
    unity_bin = '/Applications/Unity/Hub/Editor/2019.3.11f1/Unity.app/Contents/MacOS/Unity'
    proj = u.get_res('..')
    platform = u.get_env('SCRIPT_PLATFORM', '').lower()

    buildTarget = {
        'android': 'Android',
        'ios': 'iOS',
        'osx': 'OSXUniversal',
        'win': 'Win64'
    }[platform]

    args = ''
    if ext_args is not None:
        for key in ext_args:
            args += '{0} {1} '.format(key, ext_args[key])
    log_filter.start(unity_log)
    result = u.execute(unity_bin,
                        '-batchmode',
                        '-quit',
                        '-projectPath', proj,
                        '-executeMethod', 'NOAH.Build.ContinuousIntegration.' + method,
                        '-buildTarget', buildTarget, # platform.lower(),
                        '-nographics',
                        '-logFile', unity_log,
                        args,
                        ignore_error=True).code
    log_filter.stop()
    return result


def rebuild_all():
    error_code = rebuild_resource()
    if error_code == 0:
        error_code = execute_unity('BuildCode')

    return error_code


def rebuild_resource():
    error_code = execute_unity('BuildResource')
    if error_code == 0:
        def exclude(path):
            filename = u.base_name(path)
            return filename in ['AssetBundle.manifest', 'buildlog.txt', 'buildlogtep.json']
        u.sync_folder(u.get_unity_output('AssetBundle'), u.get_unity_output('AssetBundle_Repacked'), exclude_predicate=exclude)
        u.execute_module(append_resource_hash)
        if u.get_env('SCRIPT_CONFIG_DLCCLIENT') is not None:
            u.execute_module(patchlize_dlc)
        elif u.get_env('SCRIPT_CONFIG_TINYCLIENT') is not None:
            u.execute_module(patchlize_resource)
    return error_code

def refresh_uiatlas():
    return execute_unity('RefreshUIAtlas')


def main(cmd, retry=0):
    if u.get_env('SCRIPT_SKIP_UNITY_BUILD') is None:
        if u.get_env('SCRIPT_CLEANBUILD') is not None:
            u.clear_dir(u.get_unity_output(''))

        error_code = 0

        while True:
            if cmd == 'RecreateRolePrefab':
                error_code = execute_unity('RecreateRoleSprites')
                error_code = execute_unity('RecreateRolePrefab')
            elif cmd == 'RebuildAll':
                error_code = rebuild_all()
            elif cmd == 'RebuildResource':
                error_code = rebuild_resource()
            elif cmd == 'RefreshUIAtlas':
                error_code = refresh_uiatlas()
            
            if error_code == 0 or retry <= 0:
                break
            else:
                retry = retry - 1

        if error_code != 0:
            u.log(u.read(unity_log))
            u.abort()
    else:
        return 0

#!/usr/bin/python
# -*- coding: utf-8 -*-
import filecmp
import hashlib
import multiprocessing
import os
import re
import shutil
import subprocess
import sys
import time
import Queue
import json
import errno
import ctypes
import codecs
import string
import math
# import inspect

try:
    from script_extension.project_setting import proj_name
except ImportError:
    proj_name = 'NOAH'

original_env = None


class SuperFormatter(string.Formatter):
    """World's simplest Template engine."""

    def format_field(self, value, spec):
        if spec.startswith('repeat'):
            template = spec.partition(':')[-1]
            # print 'template is', template, spec.partition('|')
            def sub_str(iv):
                # print 'sub_str', template, iv, iv.name, iv.need_setter
                return self.format(template, item=iv)
            ret_temp = ''.join(sub_str(i) for i in value)
            return self.format(ret_temp,item=value)
        elif spec == 'call':
            return value()
        elif spec.startswith('?'):
            # print 'condition', value, spec[2:].partition('|')
            return spec[2:].partition('|')[0 if value else -1]
        else:
            return super(SuperFormatter, self).format_field(value, spec)
            

def initialize(script_path):
    global original_env
    original_env = os.environ.copy()

    set_script_path(dir_name(script_path))
    if is_ci_mode():
        info('Script inited: ' + get_script_path())
        info('Script running as: ' + proj_name)
        info('Command line: ' + ' '.join(sys.argv[1:]))


# Environment
def set_env(key, value):
    if value is not None and value != 'None':
        os.environ[key] = str(value)
    else:
        # os.unsetenv(key)
        os.environ[key] = 'None'
    return value


def get_env(key, default=''):
    if key in os.environ:
        value = os.environ[key]
        if value == 'None' or len(value) == 0:
            value = default
        return value
    else:
        return default


def has_env(key):
    return get_env(key) is not None


def get_user():
    return get_env('USER') or get_env('USERNAME')


def init_env(values):
    if get_env('__SCRIPT_INIT') is None:
        # if not is_ci_mode() and get_env('__SCRIPT_IGNORE_EXTERNAL_ENV') is None:
        #     reload_external_env(False)
        # if is_ci_mode():
        #     info('Init environment:')

        os.environ['PATH'] = '/usr/local/bin:' + os.environ['PATH']

        for entry in values:
            key = entry[0]
            value = entry[1]
            set_env(key, get_env(key, value))
        set_env('__SCRIPT_INIT', '1')

        build_tag = proj_name
        for tag in [
            'SCRIPT_APP_VERSION',
            'SCRIPT_APP_REVISION',
        ]:
            build_tag = build_tag + '_' + get_env(tag, 'unknown')

        user = get_user()
        build_key = user
        for key in sorted(os.environ.keys()):
            if key.startswith('SCRIPT_'):
                build_key = build_key + os.environ[key]
                if True or is_ci_mode():
                    info(key + (' ' * (30 - len(key))) + ': ' + os.environ[key])
        # build_hash = '{:08X}'.format(string_hash(build_key))
        build_hash = ''
        build_tag = build_tag + '_' + build_hash
        if is_ci_mode():
            info('Build tag: ' + build_tag)

        set_env('__SCRIPT_BUILD_TAG', build_tag)
        set_env('__SCRIPT_BUILD_HASH', build_hash)

        if write(get_temp_path('script_built_tag'), build_tag):
            if is_ci_mode():
                info('Build environment changed')
            clear_tag()


def get_original_env():
    global original_env
    return original_env


def all_env():
    return os.environ


# def update_external_env(env):
#     write(get_script('.external_env'), json.dumps(env, indent=4))
#     reload_external_env()


# def reload_external_env(overwrite=True):
#     try:
#         env = json.loads(read(get_script('.external_env')))
#         for key in env:
#             if overwrite or get_env(key) is None:
#                 set_env(key, env[key])
#     except:
#         pass


def is_darwin():
    return sys.platform.lower().startswith('darwin')


def is_win():
    return sys.platform.lower().startswith('win')


# Command line args
def get_args():
    return sys.argv[1:]


def get_arg(index, default=None):
    if type(index) is str or type(index) is unicode:
        return get_argx(index, default)
    else:
        return sys.argv[index] if index < len(sys.argv) else default


def get_argx(key, default=None):
    value = default
    for i in range(1, len(sys.argv)):
        the_arg = sys.argv[i]

        if len(key) == 1:
            match = the_arg.startswith('-') and the_arg[1:].startswith(key)
        else:
            match = the_arg.startswith('--') and the_arg[2:].startswith(key)

        if match:
            assign_index = the_arg.find('=')
            if assign_index >= 0:
                value = the_arg[assign_index + 1:]
            elif i < len(sys.argv) - 1:
                value = sys.argv[i + 1]
            else:
                value = ''
            break

    return value


def is_ci_mode():
    return get_env('__SCRIPT_CI_MODE') is not None

def is_patch_mode():
    return get_env('__SCRIPT_PATCH_MODE') is not None


# Log
LOG_LEVEL = None
LOG_LEVEL_NORMAL = 0
LOG_LEVEL_INFO = 1
LOG_LEVEL_WARNING = 2
LOG_LEVEL_ERROR = 3
LOG_LEVEL_SUCCESS = 4
LOG_LEVEL_NONE = 99
LOG_INDENT = 0


def color_message(message, color_code, bold=False):
    result = '\033[' + str(color_code) + 'm' + message + '\033[0m'
    if bold:
        result = '\033[1m' + result
    return result


def log(message, level=LOG_LEVEL_NORMAL, noident=False, bold=False):
    global LOG_INDENT, LOG_LEVEL

    original_message = message

    if LOG_LEVEL is None:
        LOG_LEVEL = int(get_env('SCRIPT_LOG_LEVEL', -1))

    if level >= LOG_LEVEL:
        is_term = get_env('TERM') is not None or get_env('USER') == '91act'

        if level == LOG_LEVEL_INFO:
            if is_term:
                message = color_message(message, 34, bold)

        if level == LOG_LEVEL_WARNING:
            message = 'warning: ' + message
            if is_term:
                message = color_message(message, 33, bold)

        if level == LOG_LEVEL_ERROR:
            message = 'error: ' + message
            if is_term:
                message = color_message(message, 31, bold)

        if level == LOG_LEVEL_SUCCESS:
            message = 'success: ' + message
            if is_term:
                message = color_message(message, 32, bold)

        if is_term:
            message = message.replace('=>', '➜').replace('<=', '✔')

        message += '\n'

        pipe = sys.stdout if level == LOG_LEVEL_NORMAL else sys.stderr

        pipe.write(('' if noident else ('  ' * LOG_INDENT)) + message)

    return original_message


def info(message, bold=False):
    return log(message, LOG_LEVEL_INFO, False, bold)


def warning(message, bold=False):
    return log(message, LOG_LEVEL_WARNING, False, bold)


def error(message, bold=False):
    set_env('__SCRIPT_ERROR', 1)
    set_env('__SCRIPT_LAST_ERROR_MESSAGE', message)
    return log(message, LOG_LEVEL_ERROR, False, bold)


# Shell
def get_val(dict, key, default=None):
    if key in dict:
        return dict[key]
    else:
        return default


def abort():
    # frame = inspect.stack(  )
    # for stack in frame:
    #     error(str(stack))
    sys.exit(-1)


def abort_if_error():
    error_code = get_env('__SCRIPT_ERROR')
    if error_code is not None:
        error('Abort due script error: ' + get_env('__SCRIPT_LAST_ERROR_MESSAGE'))
        abort()


class ExecuteResult:
    def __init__(self):
        self.code = 0
        self.out = None
        self.error = None
        self.exception = None


def execute(script, *cmd_args, **args):
    global LOG_INDENT

    ignore_error = get_val(args, 'ignore_error', False)
    verbose = get_val(args, 'verbose', True)
    work_dir = get_val(args, 'work_dir', None)
    env = get_val(args, 'env', None)

    cmd_args = list(cmd_args) + get_val(args, 'args', [])

    result = ExecuteResult()
    LOG_INDENT += 1

    current_dir = os.getcwd()
    if work_dir is not None:
        os.chdir(work_dir)

    shell = ''
    if script.endswith('.sh'):
        shell = 'bash'
    if script.endswith('.py'):
        shell = 'python'
    if len(shell) > 0:
        shell += ' '

    if is_win() and script == 'open':
        script = 'start'

    if is_ci_mode() and script == 'svn':
        cmd_args = ['--username', 'ci', '--password', 'ci'] + cmd_args

    cmd_args = [str(arg).replace('(', '\(').replace(')', '\)') for arg in cmd_args]

    cmd_line = '{0}{1} {2}'.format(shell, script, ' '.join(cmd_args))

    if verbose:
        info('=> Shell: ' + cmd_line, True)

    set_env('__SCRIPT_ERROR', None)
    start_time = time.time()

    pipes = subprocess.Popen(cmd_line, stdout=subprocess.PIPE, stderr=subprocess.PIPE, env=env, shell=True)
    result.out, result.error = pipes.communicate()
    if result.out is not None:
        result.out = result.out.strip()
    if result.error is not None:
        result.error = result.error.strip()
    result.code = pipes.returncode

    if verbose:
        info('<= Finished: {0} {1:.2f} seconds'.format(base_name(script), time.time() - start_time), True)

    if result.code != 0:
        if not ignore_error:
            if verbose:
                error('Command failed: ' + cmd_line + ' code: ' + str(result.code) + ' message: ' + result.error, True)
            abort()

    if work_dir is not None:
        os.chdir(current_dir)

    LOG_INDENT -= 1
    return result


def execute_module(module, *args):
    global LOG_INDENT

    LOG_INDENT += 1

    info('=> Module: ' + module.__name__, True)
    set_env('__SCRIPT_ERROR', None)
    start_time = time.time()
    result = apply(module.main, args)
    info('<= Finished: {0} {1:.2f} seconds'.format(module.__name__, time.time() - start_time), True)

    LOG_INDENT -= 1

    return result


# Utilities
def svn_root(path=None):
    result = None

    lines = execute('svn', 'info', path if path is not None else get_script('..'), verbose=False).out.split('\n')
    prefix = 'URL:'
    for line in lines:
        if line.startswith(prefix):
            result = line[len(prefix):].strip()

    return result

def svn_branch(path=None):
    result = None
    svnroot = svn_root(path)
    splited = svnroot.split('/branches/')
    if len(splited) >= 2:
        return splited[-1]
    return result


# Folders and files
def set_script_path(path):
    set_env('SCRIPT_PATH', path)


def get_script_path():
    return get_env('SCRIPT_PATH')

def get_native_path():
    return get_script('../NativeTemplate')

def set_res_path(path):
    set_env('SCRIPT_RES_PATH', path)

def get_res_path():
    assets = get_script('../Assets')
    print(assets)
    return get_env('SCRIPT_RES_PATH', assets)


def get_res(path):
    return join_path(get_res_path(), path)


def get_raw(path):
    return join_path(get_res('../../ResourceRaw'), path)


def get_unity_output(path):
    res_output_base = get_res('../../Output')
    platform = get_env('SCRIPT_PLATFORM', '').lower()
    if platform == 'android':
        res_output_base = join_path(res_output_base, 'Android')
    if platform == 'ios':
        res_output_base = join_path(res_output_base, 'iOS')
    if platform == 'osx':
        res_output_base = join_path(res_output_base, 'OSX')
    # if platform == 'win32':
    #     res_output_base = join_path(res_output_base, 'Windows')
    if platform == 'win':
        res_output_base = join_path(res_output_base, 'Windows')

    return join_path(res_output_base, path)


def base_name(path):
    if path is not None:
        return os.path.basename(path)


def base_name_no_ext(path):
    filename = base_name(path)
    return filename if filename.find('.') < 0 else filename[0:filename.rfind('.')]


def ext_name(path):
    index = path.rfind('.')
    if index >= 0:
        return path[index + 1:]


def file_size(path):
    return os.path.getsize(path) if os.path.exists(path) else 0


def real_path(path):
    if path is not None:
        return normalize_path(os.path.realpath(path))


def rel_path(path, base):
    return normalize_path(os.path.relpath(path, base))

def import_name(py_file, start_path):
    relative_path = rel_path(py_file, start_path)
    no_ext = os.path.splitext(relative_path)[0]
    return no_ext.replace(os.sep, '.')

def get_temp_path(path=''):
    import tempfile
    tmp_root = join_path(tempfile.gettempdir(), get_user() + '_' + get_env('JOB_NAME', 'local'))

    return join_path(tmp_root, path)


def get_script(path, resolve=True):
    if path[0] == '/':
        if resolve:
            return real_path(path)
        else:
            return path
    else:
        return join_path(get_script_path(), path, resolve)

def get_native(path, resolve=True):
    if path[0] == '/':
        if resolve:
            return real_path(path)
        else:
            return path
    else:
        return join_path(get_native_path(), path, resolve)



def get_bin(path):
    if is_darwin():
        result = join_path(get_script('bin'), 'macOS')
        result = join_path(result, path)
    elif is_win():
        result = join_path(get_script('bin'), 'win')
        result = join_path(result, path)
        result += '.exe'
    else:
        error('Unsupport platform:%s'%(sys.platform.lower()))
    return result

def which(pgm):
    path=os.getenv('PATH')
    for p in path.split(os.path.pathsep):
        p=os.path.join(p,pgm)
        if os.path.exists(p) and os.access(p,os.X_OK):
            return p

def get_bin_in_os_path(bin_name):
    return which(bin_name)

def join_path(base, path, resolve=True):
    result = base
    if path is not None:
        result = os.path.join(base, path)
    if resolve:
        result = real_path(result)
    return result


def expand_path(path):
    return os.path.expanduser(path)


def dir_name(path, resolve=True):
    if resolve:
        return os.path.dirname(real_path(path))
    else:
        return os.path.dirname(path)


def internal_get_files(path, exts=None, follow_links=True, recursive=True, ignore_hidden=True):
    result = []

    include_exts = []
    exclude_exts = []

    if exts is not None:
        include_exts = [ext for ext in exts if ext[0] != '-']
        exclude_exts = [ext[1:] for ext in exts if ext[0] == '-']

    if is_file(path):
        result = [path]
    else:
        entries = os.walk(path, followlinks=follow_links)

        if not recursive:
            try:
                entries = [next(entries)]
            except:
                entries = None

        if entries is not None:
            for root, dirs, files in entries:
                for file in files:
                    full_path = join_path(root, file)
                    is_matched = '/.svn/' not in full_path
                    is_matched = is_matched and '~$' not in full_path
                    is_matched = is_matched and (not ignore_hidden or file[0] != '.')

                    if is_matched:
                        this_ext = ext_name(file)
                        if len(include_exts) > 0: is_matched = this_ext in include_exts
                        if len(exclude_exts) > 0: is_matched = this_ext not in exclude_exts

                    if is_matched:
                        result.append(full_path)

    return result


def get_files(base_path, exts=None,
              follow_links=False,
              recursive=True,
              ignore_hidden=True,
              alt_path=None,
              prefer_alt=False):
    alt_path = join_path(base_path, alt_path)

    base_result = internal_get_files(base_path, exts, follow_links, recursive, ignore_hidden)
    alt_result = []

    if alt_path is not None and alt_path != base_path:
        alt_result = internal_get_files(alt_path, exts, follow_links, recursive, ignore_hidden)

    merge_result = base_result

    for alt_file in alt_result:
        base_file = join_path(base_path, base_name(alt_file))

        if base_file in merge_result:
            merge_result.remove(base_file)
            merge_result.append(alt_file)
        elif prefer_alt:
            merge_result.append(alt_file)

    return sorted(merge_result)


def get_files_regex(path, pattern, ignore_case=False):
    match_flag = 0
    if ignore_case:
        match_flag = re.IGNORECASE
    regex = re.compile(pattern, match_flag)

    result = []
    for root, dirs, files in os.walk(path):
        for file in files:
            if regex.match(file):
                result.append(join_path(root, file))

    return result


def search_files(paths, pattern):
    result = []

    for path in paths:
        result += get_files_regex(path, pattern)

    return result


def is_file(path):
    return os.path.isfile(path)


def is_dir(path):
    return os.path.isdir(path)


def exists(path):
    return os.path.exists(path)


def normalize_path(path):
    if is_win():
        return path.replace('/', os.sep)
    else:
        return path.replace('\\', os.sep)


def is_link(path):
    if is_win():
        result = execute('fsutil', 'reparsepoint', 'query', path, verbose=False, ignore_error=True)
        return result.code == 0
    else:
        return os.path.islink(path)    

def unlink(link):
    if is_win():
        if is_link(link):
            if is_dir(link):
                execute('rmdir', link, verbose=False)
            else:
                del_file(link)
    else:
        if os.path.islink(link):
            os.unlink(link)
            info('Unlinked => ' + link)


def link(src, dst, override=False):
    src = real_path(src)
    # dst = real_path(dst)
    if is_win():
        try:
            src = normalize_path(src)
            dst = normalize_path(dst)
            if is_link(dst):
                unlink(dst)
            else:
                if is_file(dst):
                    del_file(dst)
                if is_dir(dst):
                    del_dir(dst)

            args = []
            if is_dir(src):
                args.append('/D')
            args.append(dst)
            args.append(src)
            execute('mklink', args=args)
        except Exception as e:
            print e
            pass
    else:
        if override and os.path.exists(dst):
            if os.path.islink(dst):
                os.unlink(dst)
            else:
                os.remove(dst)
            info('Linked overrided => ' + dst)
        
        if not os.path.exists(dst):
            if src != dst:
                mkdir_for_file(dst)
                os.symlink(src, dst)
                info('Linked => ' + dst)
            else:                
                warning("Link Skip => Try to link file with the same path: " + src)
        else:       
            warning("Link Skip => Destination existed: " + dst)


def del_file(file):
    if is_file(file):
        os.remove(file)
        info('Removed => ' + file)


def del_files(files):
    for file in files:
        del_file(file)


def mkdir(folder):
    if not os.path.exists(folder):
        try:
            os.makedirs(folder)
        except:
            pass
        info('Made folder => ' + folder)


def mkdir_for_file(file):
    mkdir(dir_name(file))


def get_dirs(path, recursive=False, ignore_hidden=True):
    result = []
    if os.path.isdir(path):
        sub_dirs = [join_path(path, name)
                  for name in os.listdir(path)
                  if os.path.isdir(join_path(path, name)) and (not ignore_hidden or name[0] != '.')]
        result += sub_dirs
        if recursive:
            for e in sub_dirs:
                result += get_dirs(e, recursive, ignore_hidden)
    return result


def del_dir(folder):
    if os.path.isdir(folder):
        shutil.rmtree(folder, ignore_errors=True)
        info('Removed => ' + folder)


def clear_dir(folder):
    if os.path.isdir(folder):
        del_dir(folder)

    mkdir(folder)


def remove_empty_dirs(folder):
    if os.path.isdir(folder) and '.svn' not in folder:
        for file in os.listdir(folder):
            sub_folder = join_path(folder, file)
            if os.path.isdir(sub_folder):
                remove_empty_dirs(sub_folder)

        if len(os.listdir(folder)) == 0:
            os.rmdir(folder)


def touch(path):
    mkdir_for_file(path)
    with open(path, 'a'):
        os.utime(path, None)
        info('Touched => ' + path)


def copy(src_path, dst_path):
    if os.path.exists(src_path):
        mkdir_for_file(dst_path)
        try:
            shutil.copy(src_path, dst_path)
            shutil.copystat(src_path, dst_path)
            info('Copied => ' + dst_path)
        except:
            warning('Copystat failed => ' + dst_path)

def copytree(src_path, dst_path):
    if os.path.exists(src_path):
        mkdir_for_file(dst_path)
        try:
            shutil.copytree(src_path, dst_path)
            info('Copied folder => ' + dst_path)
        except:
            warning('Copied folder failed => ' + dst_path)


def move(src_path, dst_path):
    if os.path.exists(src_path) and not os.path.exists(dst_path):
        shutil.move(src_path, dst_path)
        info('Moved => ' + dst_path)

def compare_mtime_impl(src, dst):
    return os.path.getmtime(src) - os.path.getmtime(dst) > 1

def compare_mtime(dst_file, src_files):
    result = get_env('__SCIPRT_FORCE_MTIME_COMPARE_TRUE') is not None or not exists(dst_file)
    if not result:
        for file in src_files:
            if compare_mtime_impl(file, dst_file):
                result = True
                break

    return result


def write(path, content, force=False):
    need_update = force or not exists(path)
    if not need_update:
        with open(path, 'rb') as file:
            need_update = file.read() != content

    if need_update:
        mkdir_for_file(path)
    
        file_attr = 0
        FILE_ATTRIBUTE_HIDDEN = 0x02
        FILE_ATTRIBUTE_READONLY = 0x01
        attr_mask = FILE_ATTRIBUTE_HIDDEN | FILE_ATTRIBUTE_READONLY

        if is_win() and exists(path):
            file_attr = ctypes.windll.kernel32.GetFileAttributesA(path)
            ctypes.windll.kernel32.SetFileAttributesA(path, file_attr & ~attr_mask)

        with open(path, 'wb') as file:
            file.write(content)

        if is_win() and file_attr > 0:
            ctypes.windll.kernel32.SetFileAttributesA(path, file_attr)

    return need_update


def read(path, skip_bom=False):
    result = ''
    if exists(path):
        with open(path, 'rb') as file:
            result = file.read().replace('\r', '')
    else:
        warning("Failed when try to read [%s] "%(path))
    if skip_bom:
        result = result.decode('utf-8-sig')
    return result


def normalize_eol(path):
    if exists(path):
        content = read(path)
        content = content.replace('\r\n', '\n').replace('\r', '\n')
        write(path, content)


def strip_bom(path):
    content = read(path)

    if content is not None and len(content) >= 3 and content[:3] == codecs.BOM_UTF8:
        content = content[3:]
        write(path, content)    

def dev_null():
    if is_win():
        return 'NUL'
    else:
        return '/dev/null'


def tag_path():
    return get_temp_path('script_tag_' + get_env('__SCRIPT_BUILD_HASH'))


def gen_tag(tag):
    return join_path(tag_path(), tag)


def clear_tag():
    del_dir(tag_path())


def sync_folder_symbol(src_path, dst_path,
                       src_files=None,
                       exclude_predicate=None,
                       recursive=True,
                       exts=None):
    src_path = real_path(src_path)

    dst_path = real_path(dst_path)
    if src_files is None:
        src_files = get_files(src_path, recursive=recursive, exts=exts)
    else:
        src_files = [join_path(src_path, file) for file in src_files]

    if exclude_predicate is not None:
        src_files = [file for file in src_files if not exclude_predicate(file)]
    for file in src_files:
        rel_path = os.path.relpath(file, src_path)
        dst_file = join_path(dst_path, rel_path, resolve=False)  # Never resolve source path while using symbol link.
        if not exists(dst_file):
            mkdir_for_file(dst_file)
        link(file, dst_file, override=True)
    return src_files


def sync_folder(src_path, dst_path,
                src_files=None,
                remove_diff=True,
                compare_content=False,
                diff_predicate=None,
                exclude_predicate=None,
                exts=None,
                remove_original=False):
    src_path = real_path(src_path)

    dst_path = real_path(dst_path)
    if src_files is None:
        src_files = get_files(src_path, exts)
    else:
        src_files = [join_path(src_path, file) for file in src_files]

    if exclude_predicate is not None:
        src_files = [file for file in src_files if not exclude_predicate(file)]

    if remove_diff:
        dst_files = get_files(dst_path)
        for file in dst_files:
            rel_path = os.path.relpath(file, dst_path)
            if not is_file(join_path(src_path, rel_path)) and (diff_predicate is None or diff_predicate(file)):
                del_file(file)

        remove_empty_dirs(dst_path)

    for file in src_files:
        rel_path = os.path.relpath(file, src_path)
        # dst_file = join_path(dst_path, os.path.basename(file) if ignore_dstrel else rel_path)
        dst_file = join_path(dst_path, rel_path)
        if is_file(file):
            need_sync = not is_file(dst_file)
            if not need_sync:
                if compare_content:
                    need_sync = not filecmp.cmp(file, dst_file)
                else:
                    need_sync = compare_mtime_impl(file, dst_file)
            if need_sync:
                copy(file, dst_file)
            if remove_original:
                del_file(file)
        elif is_dir(file):
            copytree(file, dst_file)
            if remove_original:
                del_file(file)
        else:
            warning('Not found => ' + file)

    return src_files


def file_hash(files):
    hasher = hashlib.new('sha1')

    for file in files:
        with open(file, 'rb') as input_file:
            hasher.update(input_file.read())

    return hasher.hexdigest()


def zip_files(path, pack_size=4 * 0x100000, filenametag=''):
    pwd = os.getcwd()
    os.chdir(path)

    zip_packs = []

    uncompressed_files = []
    uncompressed_size = 0

    for file in sorted(get_files(path), key=lambda a: file_size(a)):
        uncompressed_size += file_size(file)

        if uncompressed_size <= pack_size:
            uncompressed_files.append(file)
        else:
            zip_packs.append(uncompressed_files)
            uncompressed_size = file_size(file)
            uncompressed_files = [file]

    zip_packs.append(uncompressed_files)

    zip_index = 0
    timestamp = int(time.time())
    for uncompressed_files in zip_packs:
        if len(uncompressed_files) > 0:
            list_file = get_temp_path('pack_list')
            write(list_file, '\n'.join([rel_path(file, path) for file in uncompressed_files]))            
            execute('zip', '-q', 'update_{0}_{1}_{2}_{3}'.format(timestamp, filenametag, zip_index, file_hash(uncompressed_files)), '-@', '<', list_file)
            zip_index += 1

    os.chdir(pwd)


def mount(mount_info):
    (protocol, user, password, remote, path) = mount_info

    local = None

    mount_point = '//{}@{}/{}'.format(user, remote, path)

    for mount_entry in execute('mount').out.split('\n'):
        if mount_entry.startswith(mount_point):
            local = mount_entry.split(' ')[2]
            break

    if local is None:
        local = join_path(expand_path('~/.mount'), path)
        if not os.path.isdir(local) or not os.path.ismount(local):
            mkdir(local)
            execute('mount_' + protocol, '{}://{}:{}@{}:/{}'.format(protocol, user, password, remote, path), local)

    return local


def umount(mount_point):
    execute('umount', mount_point, ignore_error=True)


def readable(size):
    unit = ['Byte', 'KB', 'MB', 'GB', 'TB']
    index = 0
    size = float(size)
    while size >= 1024 and index < len(unit):
        size /= 1024
        index += 1

    return '{:.2f} {}'.format(size, unit[index])


# def deploy(src_path, dst_path, files, remove_diff=True):
#     info('deploy begin: %s => %s, %s files'%(src_path,dst_path,len(files)) )
#     synced_files = sync_folder(src_path, dst_path, src_files=files, remove_diff=remove_diff)
#     total_size = sum([file_size(file) for file in synced_files])
#     info('Deployed {} files, total size: {}({})'.format(len(synced_files), readable(total_size), total_size))

#     with open(join_path(dst_path, '.meta'), 'w+') as meta_output:
#         import json

#         dict = {}

#         for key in os.environ:
#             dict[key] = os.environ[key]

#         meta_output.write(json.dumps(dict, sort_keys=True, indent=4))


def pool_function(args):
    try:
        args[0](*args[1])
    except Exception as e:
        raise


def parallel_simple(func, args_list, threads=0):
    parallel(func, [[args] for args in args_list], threads)


def parallel(func, args_list, threads=0):
    if is_win():
        for args in args_list:
            apply(func, args)
    else:
        if threads == 0:
            threads = multiprocessing.cpu_count()

        pool = multiprocessing.Pool(threads)
        p = pool.map_async(pool_function, [[func, args] for args in args_list])

        try:
            p.get(0xFFFF)
        except Exception as e:
            raise e


def is_same_file(path_array):
    result = False

    for i in range(len(path_array)):
        for j in range(i + 1, len(path_array)):
            is_same = filecmp.cmp(path_array[i], path_array[j])
            if is_same:
                result = True
                break
    return result


# def string_hash(value):
#     import PythonExt
#     return PythonExt.string_hash(value) & 0xFFFFFFFF


def pprint(obj):
    import pprint
    pprint.pprint(obj)


def get_attr(instance, name):
    try:
        return getattr(instance, name)
    except:
        return None


def clean_workspace(work_space=None):
    if work_space is None:
        work_space = real_path(get_env('WORKSPACE'))

    if work_space is not None:
        execute(get_bin('clean_workspace.sh'), work_space)
    
    clear_tag()


def preprocess_code(source):
    cc = get_env('CC', 'clang')
    return execute(
        cc,
        '-x c',  # Consider input file as c source
        '-E -P',  # Preprocess only without any other comments
        '-Wno-invalid-pp-token',
        source,
        verbose=False).out


def merge_object(to_dict, from_dict, repalce=True):
    for key in from_dict:
        from_value = from_dict[key]
        to_value = to_dict[key]
        if isinstance(to_value, dict) and isinstance(from_value, dict):
            merge_object(to_value, from_value)
        elif isinstance(to_value, list) and isinstance(from_value, list):
            to_value += from_value
        else:
            to_dict[key] = from_value
    return to_dict


def compare_array(array_a, array_b):
    result = len(array_a) == len(array_b)
    if result:
        for i in range(0, len(array_a)):
            if array_a[i] != array_b[i]:
                result = False
                break

    return result


def last_commit(path):
    result = execute('svn', 'log', '-l 1', path, verbose=False)
    if result.code == 0:
        return result.out.split('\n')[1].split('|')[1].strip()

def version_compare(version_a, version_b):
    segs_a = [int(s) for s in version_a.split('.')]
    segs_b = [int(s) for s in version_b.split('.')]

    for i in range(0, len(segs_a)):
        if i < len(segs_b):
            seg_a = segs_a[i]
            seg_b = segs_b[i]

            if seg_a > seg_b:
                return 1
            elif seg_a < seg_b:
                return -1
        else:
            return 1

    return 0

def float_to_fixed(f):
    return (int(math.floor(f)) << 32) + int(math.modf(f)[0] * (1 << 32))

def fixed_to_float(f):
    return (int(f) & 0xFFFFFFFF) / float(1L << 32) + (int(f) >> 32)

#!/usr/bin/python

from common import utility as u
import re
import time,os

def get_bin():    
    return u.get_bin('protoc')

def get_protofiles(input_path):
    proto_files = []
    for path in input_path:
        proto_files += [f for f in u.get_files(path, ['proto'], recursive=True) if 'google' not in f]

    return proto_files


def make_args(input_path, search_path, purpose):    
    proto_files = get_protofiles(input_path)

    if len(proto_files) > 0:
        additional_path = u.get_temp_path("cmd_args")
        u.write(additional_path, "\n".join(proto_files))
        args = ''        
        args = args + '--strip_source_info '
        args = args + '--ignore_options=urls:view:comment:fc:ec:evc '
        if u.is_ci_mode():
            args = args + '--ignore_options=NOAH.Proto.enum_tooltip:NOAH.Proto.field_tooltip '
        args = args + ' '.join(['--proto_path=' + p for p in search_path if u.exists(p)]) + ' @' + additional_path
        
        return args
    else:
        return None, None

def generate_descriptor(input_path, pb_out, search_path):
    args = make_args(input_path, search_path, 'descriptor')   
    u.execute(get_bin(), '--include_source_info', '-o ' + pb_out, args)


def generate_python(input_path, python_out, search_path):
    args = make_args(input_path, search_path, 'python')   
    u.clear_dir(python_out)
    u.execute(get_bin(), '--python_out=' + python_out, args)
    u.touch(u.join_path(python_out, '__init__.py'))

    # HACK: Temporary fix for windows since it would cause error if a proto file is too large
    for file in u.get_files(python_out, ['py']):
        content = u.read(file)
        content = content.replace('serialized_options=None', 'serialized_options=\'\'')
        u.write(file, content)



def generate_csharp(input_path, csharp_out, search_path):
    args = make_args(input_path, search_path, 'csharp')   
    u.del_files(u.get_files(csharp_out, ['cs']))        
    u.execute(get_bin(), '--csharp_out=' + csharp_out, args)

    for file in u.get_files(csharp_out, ['cs']):
        u.normalize_eol(file)



def generate_go(input_path, go_out, search_path):
    args = make_args(input_path, search_path, 'go')   
    u.mkdir(go_out)
    u.del_files(u.get_files(go_out, ['go']))
    u.execute(get_bin(),
                '--go_out=' + go_out,
                '-I ' + input_path[0],
                input_path[0] + '/*.proto')

                    
def do_compile():
    # for project
    proj_input_path = u.get_res('../')
    print(proj_input_path)
    if not u.exists(proj_input_path):
        u.mkdir(proj_input_path)
    proj_csharp_out = u.get_res('../Generated/')
    if not u.exists(proj_csharp_out):
        u.mkdir(proj_csharp_out)
    generate_csharp([proj_input_path], proj_csharp_out, [proj_input_path])


def main():
    do_compile()


if __name__ == '__main__':
    main()

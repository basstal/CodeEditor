#!/usr/bin/python

from common import utility as u


def main(app, src_path):
    deploy_files = u.get_files(src_path, ['zip'])
    deploy_files.append(u.join_path(src_path, '.summary'))

    mount_point = u.mount(('afp', 'ci', 'cicicici', 'nas.91act.com', '91ACT_CONTROL'))
    deploy_path = app.next_patch(mount_point, u.get_env('SCRIPT_PATCH_NEXT_PATCH', '1'))
    u.deploy(src_path, deploy_path, deploy_files)
    u.umount(mount_point)

    u.execute('curl',
              '-H "Content-Type: application/json"',
              '-X POST',
              '--data {"CIPATH":"' + deploy_path + '"}',
              'http://10.10.5.94:11800/api/v1.0/ci',
              ignore_error=True)

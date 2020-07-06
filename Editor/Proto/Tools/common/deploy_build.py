#!/usr/bin/python

import re

from common import utility as u
from common.app_info import AppInfo


def upload_envs(src_path, platform):
    if platform == 'android':
        # Read package info from the APK file
        if u.get_env('SCRIPT_ANDROID_APP_BUNDLE') is not None:
            input_path = u.join_path(src_path, u.proj_name + '.aab')
            bundletool = u.get_script('android/bundletool.jar')
            package_str = u.execute('java', '-jar', bundletool, 'dump', 'manifest', '--bundle=' + input_path, '| grep package=').out
            package_reg = re.compile(r'.*package="(?P<name>[^"]*)".*', re.X | re.S)
        else:
            input_path = u.join_path(src_path, u.proj_name + '.apk')
            package_str = u.execute('aapt', 'dump', 'badging', input_path, '| grep package:').out
            package_reg = re.compile(r"package:\s+name='(?P<name>[^']*)'\s+versionCode='(?P<versionCode>[^']*)'\s+versionName='(?P<versionName>[^']*)'", re.X | re.S)

        match = re.match(package_reg, package_str).groupdict()
        bid = match['name']
        u.set_env('SCRIPT_ANDROID_PACKAGENAME', bid)

    if platform == 'ios':
        # Extract the Info.plist from IPA file
        u.execute('unzip', '-p', u.join_path(src_path, u.proj_name + '.ipa'), 'Payload/'  + u.proj_name + '.app/Info.plist', '> Info.plist')
        # Read package info from the Info.plist file
        bid = u.execute('/usr/libexec/PlistBuddy', '-c "Print :CFBundleIdentifier"', 'Info.plist', verbose=False).out
        display_name = u.execute('/usr/libexec/PlistBuddy', '-c "Print :CFBundleDisplayName"', 'Info.plist', verbose=False).out
        version_name = u.execute('/usr/libexec/PlistBuddy', '-c "Print :CFBundleShortVersionString"', 'Info.plist', verbose=False).out
        version_code = u.execute('/usr/libexec/PlistBuddy', '-c "Print :CFBundleVersion"', 'Info.plist', verbose=False).out
        u.del_file('Info.plist')

        version = '{}({})'.format(version_name, version_code)
        # For download page
        u.set_env('__SCRIPT_IPA_BUNDLEID', bid)
        u.set_env('__SCRIPT_IPA_VERSIONNAME', version)
        u.set_env('__SCRIPT_IPA_DISPLAYNAME', display_name)


def main(src_path, platform, files):
    upload_envs(src_path, platform)

    mount_point = u.mount(('afp', 'ci', 'cicicici', 'nas.91act.com', '91ACT_CONTROL'))

    app = AppInfo(
        u.get_env('SCRIPT_PLATFORM').lower(),
        u.get_env('JOB_NAME'),
        u.get_env('SCRIPT_CONFIG_GAMEID'),
        u.get_env('SCRIPT_CONFIG_CHANNELID'),
        u.get_env('SCRIPT_APP_VERSION'),
        u.get_env('SCRIPT_APP_REVISION'),
        0,
        u.get_env('SVN_REVISION', u.get_env('SVN_REVISION_1', 0))
    )
    

    dst_path = app.archive_root(mount_point)
    u.deploy(src_path, dst_path, files, False)
    for file in [u.proj_name + '.apk', u.proj_name + '.aab', u.proj_name + '.ipa']:
        full_path = u.join_path(dst_path, file)
        renamed_path = u.join_path(dst_path, file.replace(u.proj_name, u.get_env('__SCRIPT_BUILD_TAG')))
        u.del_file(renamed_path)
        u.move(full_path, renamed_path)
    u.umount(mount_point)

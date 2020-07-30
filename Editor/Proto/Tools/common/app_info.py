from common import utility as u


class AppInfo:
    def __init__(self, platform, job_name, game_id, channel_id, app_version, app_revision, app_patch, svn_revision, external_rev_xlsx=None, external_rev_localization=None):
        self.platform = platform
        self.job_name = job_name
        self.game_id = game_id
        self.channel_id = channel_id
        self.app_version = app_version
        self.app_revision = app_revision
        self.app_patch = app_patch
        self.svn_revision = svn_revision
        self.svn_external = {
            'Sources/ExcelData/xlsx': external_rev_xlsx,
            'Sources/ExcelData/localization': external_rev_localization,
        }

    def archive_root(self, mount_point):
        return ('{}/depot/' + u.proj_name.lower() + '/{}/{}/{}').format(mount_point, self.platform, self.job_name, self.svn_revision)

    def archive(self, mount_point):
        candidates = [f for f in u.get_files(self.archive_root(mount_point), self.archive_ext()) if u.base_name(f).startswith(self.archive_prefix())]
        if len(candidates) > 0:
            return candidates[0]

    def patch_root(self, mount_point):
        return ('{}/clientupdate/{}/{}/{}.{}').format(mount_point, 
        self.game_id, 
        self.channel_id, 
        self.app_version, 
        self.app_revision)

    def patches(self, mount_point):
        all_patch_folders = u.get_dirs(self.patch_root(mount_point))
        patch_files = []
        iter_patch = self.app_patch
        while iter_patch > 0:
            suffix = '_{}'.format(iter_patch)
            for folder in all_patch_folders:
                if folder.endswith(suffix):
                    patch_files = u.get_files(u.join_path(folder, self.platform), ['zip'], recursive=False) + patch_files
                    iter_patch = int(u.base_name(folder).split('_')[0])
                    break

        return patch_files

    def next_patch(self, mount_point, to_patch):
        return ('{}/{}_{}/{}').format(self.patch_root(mount_point), self.app_patch, to_patch, self.platform)

    def dlc_path(self,mount_point):
        return ('{}/dlc').format(self.patch_root(mount_point))
        
    def dlc_patch_path(self,mount_point,to_patch):
        return ('{}/dlc').format(self.next_patch(mount_point,to_patch))
        
    def streaming_folder(self):
        result = None
        if self.platform == 'ios':
            result = 'Payload/' + u.proj_name + '.app/Data/Raw'
        else:
            if u.get_env('SCRIPT_ANDROID_APP_BUNDLE') is not None:
                result = 'base/assets'
            else:
                result = 'assets'
        return result

    def archive_ext(self):
        if self.platform == 'ios':
            return ['ipa']
        else:
            return ['apk', 'aab']

    def archive_prefix(self):
        return u.proj_name + '_{}_{}'.format(self.app_version, self.app_revision)

    def update_custom_external(self):
        for k, v in self.svn_external.iteritems():
            if v:
                u.execute('svn', 'up', u.get_res(k), '-r', v)


from common import utility as u


def decode_provision(path):
    result = []
    files = u.get_files(u.expand_path('~/Library/MobileDevice/Provisioning Profiles'), ['mobileprovision'])
    for file in files:
        result.append(u.execute('/usr/libexec/PlistBuddy',
                                '-c "Print ' + path + '"',
                                '/dev/stdin <<<',
                                '`security cms -D -i "{}" 2>/dev/null`'.format(file),
                                verbose=False))

    return result


def list_provision():
    print '\n'.join(decode_provision(':Name'))


def list_bundleid():
    print '\n'.join([bid[11:] for bid in decode_provision(':Entitlements:application-identifier')])


if __name__ == '__main__':
    command = u.get_arg(1)
    if command == 'provision':
        list_provision()
    if command == 'bundleid':
        list_bundleid()

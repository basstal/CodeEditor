#!/usr/bin/python
# -*- coding: utf-8 -*-

import utility as u

defaults = [
    ['SCRIPT_APP_VERSION', '1.0'],
    ['SCRIPT_APP_REVISION', u.get_env('SVN_REVISION', u.get_env('SVN_REVISION_1', 0))],
]

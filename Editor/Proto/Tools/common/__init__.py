import sys

import utility as u
from common import defaults

reload(sys)
sys.setdefaultencoding('utf-8')

sys.path.append(u.join_path(u.dir_name(__file__), '../bin'))

u.initialize(u.dir_name(__file__))

u.init_env(defaults.defaults)
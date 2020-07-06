from common import utility as u
import sys
from select import select
import time
import thread
import os

def halt(message=None):
    if message:
        u.error(message)
    u.execute('killall', 'Unity')

is_monitoring = False
monitor_path = None
HALT_TIMEOUT = 3600 #seconds
time_out = HALT_TIMEOUT
error_code = 0

def start(path):
    global is_monitoring,monitor_path
    if is_monitoring:
        u.warning("logfilter is monitoring at [%s]"%(monitor_path))
    is_monitoring = True
    monitor_path = path
    try:
        thread.start_new_thread(monitor_impl,(monitor_path,))
    except Exception as e:
        u.error("Error: unable to start thread:%s"%(e))
        u.abort()

def monitor_impl(path):
    global time_out,error_code
    error_code = 0
    time_out = HALT_TIMEOUT
    
    if not os.path.isfile(path):
        
        u.info('touching file at [%s]'%(path))
        basedir = os.path.dirname(path)
        if basedir not in ['',' ','/']:
            if not os.path.exists(basedir):
                os.makedirs(basedir)
        with open(path, 'a'):
            os.utime(path, None)
            u.info("logfilter: new logfile was touched in [%s]"%(path))
    u.info("logfilter starting tracking:%s"%(path))
    tracking_file = open(path,"r")
    loglines = follow(tracking_file)
    for line in loglines:
        for error_msg in ['Receiving unhandled NULL exception', 
        'Launching bug reporter',
        'Aborting batchmode due to failure',
        "Assertion failed on expression"]:
            if error_msg in line:
                halt("Unity crashed or failed")
                error_code = -1
                break
        if '=== Build Resource Begin ===' in line:
            time_out = HALT_TIMEOUT
        if '=== Build Player Begin ===' in line:
            time_out = HALT_TIMEOUT
    if error_code != 0:
        u.abort()

def stop():
    global is_monitoring
    is_monitoring = False

def follow(thefile):
    global is_monitoring,error_code
    thefile.seek(0,2)
    waiting_time = 0 #second
    while True:
        if not is_monitoring:
            break
        line = thefile.readline()
        if not line:
            if waiting_time >= time_out:
                halt("Unity hanged for a long time %s seconds"%(waiting_time))
                error_code = -2
                break
            waiting_time += 5
            time.sleep(5)
            continue
        else:
            waiting_time = 0
        yield line

def test():
    log_path = "log/test.log"
    start(log_path)
    u.execute("test.sh")
    stop()

if __name__ == '__main__':
    # main()
    test()

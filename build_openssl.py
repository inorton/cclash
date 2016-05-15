#!/usr/bin/python
"""
Build openssl
"""
import os
import sys
import shutil
import time
import subprocess

OSLVERSION = "1.0.2g"
THISDIR = os.path.dirname(os.path.abspath(__file__))
DIST = "Release"
STARTSERVER = True
STATS = True
TRACKER = "no"


def envcheck(bindir=None):
    """
    Check vcvars
    """
    try:
        assert "LIB" in os.environ
        assert "VCINSTALLDIR" in os.environ
    except AssertionError:
        print "Run this from a visual studio command prompt"
        sys.exit(1)
    if bindir is None:
        bindir = os.path.join(THISDIR, "CClash", "bin", DIST)
    pathvar = os.getenv("PATH")
    os.environ["PATH"] = bindir + os.pathsep + pathvar
    os.environ["CCLASH_Z7_OBJ"] = "yes"
    os.environ["CCLASH_SERVER"] = "1"
    os.environ["CCLASH_TRACKER_MODE"] = TRACKER

    cachedir = os.path.join(THISDIR, "oslcache")
    os.environ["CCLASH_DIR"] = cachedir

    if STARTSERVER:
        try:
            subprocess.check_call(["cl", "--cclash", "--stop"])
        except subprocess.CalledProcessError:
            pass
        subprocess.check_call(["cl", "--cclash", "--start"])


def build():
    """
    Build openssl using cclash
    """
    if STATS:
        print subprocess.check_output(["cl", "--cclash"])
    oslsrc = "openssl-" + OSLVERSION
    os.chdir(THISDIR)
    clean_build()

    sys.stdout.write(".. copying openssl source tree ..")
    shutil.copytree(os.path.join(THISDIR, oslsrc), 
                    os.path.join(THISDIR, "buildtemp"))
    print "done."

    os.chdir("buildtemp")

    sys.stdout.write(".. running Configure ..")
    subprocess.check_output(["perl", "Configure", "VC-WIN32", "no-asm",
                             "--prefix=c:\openssl"])
    print "done."
    
    sys.stdout.write(".. create makefiles ..")
    subprocess.check_output(["ms\\do_ms.bat"])
    print "done."
 
    sys.stdout.write(".. starting build ..")
    started = time.time()
    subprocess.check_output(["nmake", "-f", "ms\\nt.mak"])
    ended = time.time()
    print "done."
    print "total time = {}sec".format(int(ended - started))


def clean_build():
    if os.path.exists("buildtemp"):
        print ".. move earlier build.."
        repeat = 4
        while repeat > 0:
            try:
                time.sleep(20)  # antivirus might still be in here..
                os.rename("buildtemp", "buildtemp." + str(time.time()))
                repeat = 0
            except Exception as err:
                print "cant move! " + str(err)
                repeat -= 1
                if repeat == 0:
                    raise
        print ".. moved"


def try_build():
    """
    Print errors when it goes wrong
    """
    try:
        build()
    except subprocess.CalledProcessError as cpe:
        print cpe.output
        sys.exit(1)

                                       
if __name__ == "__main__":
    if "--debug" in sys.argv:
        DIST = "Debug"
    if "--no-start" in sys.argv:
        STARTSERVER = False
    if "--tracker" in sys.argv:
        TRACKER = "yes"
    bindir = None

    for item in sys.argv[1:]:
        if os.path.isdir(item):
            bindir = item
            STATS = False
            break

    envcheck(bindir)
    try_build()
    try_build()
    try_build()

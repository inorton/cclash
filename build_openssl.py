#!/usr/bin/python
"""
Build openssl
"""
import os
import sys
import shutil
import time
import subprocess

OSLVERSION="1.0.2g"

THISDIR=os.path.dirname(os.path.abspath(__file__))

def envcheck():
    """
    Check vcvars
    """
    try:
        assert "LIB" in os.environ
        assert "VCINSTALLDIR" in os.environ
    except AssertionError:
        print "Run this from a visual studio command prompt"
        sys.exit(1)
    bindir = os.path.join(THISDIR, "CClash", "bin", "Release")
    pathvar = os.getenv("PATH")
    os.environ["PATH"] = bindir + os.pathsep + pathvar
    os.environ["CCLASH_Z7_OBJ"] = "yes"
    os.environ["CCLASH_SERVER"] = "1"
    try:
        subprocess.check_call(["cl", "--cclash", "--stop"])
    except:
        pass
    subprocess.check_call(["cl", "--cclash", "--start"])

    cachedir = os.path.join(THISDIR, "oslcache")
    if os.path.isdir(cachedir):
        shutil.rmtree(cachedir)
    os.makedirs(cachedir)
    os.environ["CCLASH_DIR"] = cachedir


def build():
    """
    Build openssl using cclash
    """
    oslsrc = "openssl-" + OSLVERSION
    os.chdir(THISDIR)
    if os.path.exists("buildtemp"):
        shutil.rmtree("buildtemp")
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
    envcheck()
    try_build()
    try_build()
    try_build()

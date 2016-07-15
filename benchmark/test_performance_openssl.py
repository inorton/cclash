#!/usr/bin/python
"""
A py.test test that attempts to build openssl and benchmark the effect of clcache
"""
import sys
import os
import shutil
import pytest
import urllib
import zipfile
import subprocess
import time


OPENSSL_ZIP = "OpenSSL_1_0_2-stable.zip"
OPENSSL_URL = "https://codeload.github.com/openssl/openssl/zip/OpenSSL_1_0_2-stable"

THISDIR = os.path.dirname(os.path.abspath(__file__))
DISTDIR = os.path.join(os.path.dirname(THISDIR), "dist")
CLCACHE = os.path.join(DISTDIR, "cl.exe")
SOURCES = []
ENVS = dict(os.environ)
os.chdir(THISDIR)


def retry_delete(path):
    """
    Repeatedly attempt to delete path
    :param path:
    :return:
    """
    for _ in range(30):
        # antivirus might be busy in here..
        try:
            shutil.rmtree(path)
            return
        except WindowsError:
            time.sleep(2)
        if os.path.exists(path):
            raise Exception("could not delete {}".format(path))


class BlockMessage(object):
    """
    Little class to emit "begin .. end" messages for a block of code
    """

    def __init__(self, message):
        self.started = 0
        self.message = message

    def __enter__(self):
        self.started = time.time()
        print "\n..begin {} .. ".format(self.message)

    def __exit__(self, exc_type, exc_val, exc_tb):
        result = "OK"
        if exc_val is not None:
            result = "ERROR"
        print "\n..end {} {}.. ({}sec)".format(self.message, result,
                                               time.time() - self.started)


def download_openssl():
    """
    Get the openssl zip and unpack it
    """
    if not os.path.exists(OPENSSL_ZIP):
        getoslsrc = urllib.URLopener()
        with BlockMessage("download openssl"):
            getoslsrc.retrieve(OPENSSL_URL, OPENSSL_ZIP + ".part")
            os.rename(OPENSSL_ZIP + ".part", OPENSSL_ZIP)


def clean_openssl_build():
    """
    Unpack the openssl source, possibly deleting the previous one
    :return:
    """
    with zipfile.ZipFile(OPENSSL_ZIP, "r") as unzip:
        folder = unzip.namelist()[0]
        if os.path.exists(folder):
            with BlockMessage("delete old openssl folder"):
                retry_delete(folder)

        with BlockMessage("unzip openssl"):
            unzip.extractall()

        if len(SOURCES) == 0:
            SOURCES.append(folder.rstrip("/"))


def find_visual_studio():
    """
    Attempt to find vs 11 or vs 12
    :return:
    """
    vcvers = ["13.0", "12.0", "11.0"]
    for vc in vcvers:
        vcdir = os.path.join("c:\\", "Program Files (x86)",
                             "Microsoft Visual Studio {}".format(vc),
                             "VC", "bin")
        vcvars = os.path.join(vcdir, "vcvars32.bat")
        if os.path.exists(vcvars):
            return vcdir, vcvars

    raise Exception("cannot find visual studio!")


def configure_openssl():
    """
    Run the configure steps (requires perl)
    :return:
    """
    with BlockMessage("configure openssl"):
        subprocess.check_call(["perl",
                               "Configure", "VC-WIN32", "no-asm", "--prefix=c:\openssl"],
                              env=ENVS,
                              cwd=SOURCES[0])

    with BlockMessage("generate makefiles"):
        subprocess.check_call([os.path.join("ms", "do_ms.bat")],
                              shell=True,
                              env=ENVS,
                              cwd=SOURCES[0])


def setup_function(request):
    """
    Ensure a clean build tree before each test
    :return:
    """
    clean_openssl_build()
    configure_openssl()


def get_vc_envs():
    """
    Get the visual studio dev env vars
    :return:
    """
    _, vcvars = find_visual_studio()
    with BlockMessage("getting vc envs"):
        getenvs = subprocess.check_output([os.path.join(THISDIR, "get_vc_envs.bat"), vcvars])
        for line in getenvs.splitlines():
            if "=" in line:
                name, val = line.split("=", 1)
                ENVS[name.upper()] = val


def setup_module():
    """
    Check that our exe has been built.
    :return:
    """
    if not os.path.isfile(CLCACHE):
        pytest.fail("please build the exe first")
    get_vc_envs()
    download_openssl()


def replace_wipe_cflags(filename):
    """
    Open the nmake file given and turn off PDB generation for .obj files
    :param filename:
    :return:
    """
    lines = []
    with open(filename, "rb") as makefile:
        for line in makefile.readlines():
            if line.startswith("APP_CFLAG="):
                lines.append("APP_CFLAG=")
            elif line.startswith("LIB_CFLAG="):
                lines.append("LIB_CFLAG=/Zl")
            else:
                lines.append(line.rstrip())

    with open(filename, "wb") as makefile:
        for line in lines:
            makefile.write(line + "\r\n")


def build_openssl(addpath=None, envs=ENVS, pdbs=False):
    """
    Build openssl, optionally prefixing addpath to $PATH
    :param addpath:
    :param envs: env var dict to use
    :param pdbs: if False, turn off pdb generation in the makefile
    :return:
    """
    nmakefile = os.path.join("ms", "nt.mak")
    if not pdbs:
        replace_wipe_cflags(os.path.join(SOURCES[0], nmakefile))

    if addpath is not None:
        envs["PATH"] = addpath + os.pathsep + envs["PATH"]

    try:
        with BlockMessage("running nmake"):
            subprocess.check_output(["nmake", "-f", nmakefile],
                                    shell=True,
                                    env=envs,
                                    cwd=SOURCES[0])
    except subprocess.CalledProcessError as cpe:
        print cpe.output
        raise


def setup_clcache_envs():
    """
    return a dict of envs suitable for clcache to work with
    :return:
    """
    envs = dict(ENVS)
    vcdir, _ = find_visual_studio()
    cachedir = os.path.join("clcache_cachedir")
    envs["CLCACHE_DIR"] = cachedir
    envs["CLCACHE_CL"] = os.path.join(vcdir, "cl.exe")
    return envs


def test_build_nocache():
    """
    Time an openssl build with no caching involved at all
    :return:
    """
    build_openssl(None)


def test_build_withclcache_00_cold():
    """
    Time an openssl build with a cold cache
    :return:
    """
    envs = setup_clcache_envs()
    retry_delete(envs["CLCACHE_DIR"])
    build_openssl(DISTDIR, envs)
    test_build_withclcache_00_cold.success = True
test_build_withclcache_00_cold.success = False


def test_build_withclcache_01_warm():
    """
    Time an openssl build with a warm cache
    :return:
    """
    assert test_build_withclcache_00_cold.success, "must run test_build_withclcache_00_cold first"
    envs = setup_clcache_envs()
    build_openssl(DISTDIR, envs)


if __name__ == "__main__":
    pytest.main(sys.argv[1:])

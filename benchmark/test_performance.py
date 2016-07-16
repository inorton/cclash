"""
Test cclash speed
"""
import sys
import os
import pytest
import subprocess
import threading
import test_performance_openssl as tpo

THISDIR = os.path.dirname(os.path.abspath(__file__))
CCLASH_BIN = os.path.join(os.path.dirname(THISDIR), "cclash", "bin", "debug")
CCLASH_EXE = os.path.join(CCLASH_BIN, "cl.exe")
if not os.path.exists(CCLASH_EXE):
    CCLASH_BIN = os.path.join(os.path.dirname(THISDIR), "cclash", "bin", "release")
    CCLASH_EXE = os.path.join(CCLASH_BIN, "cl.exe")


def run_server():
    """
    Run the cclash server
    :return:
    """
    envs = setup_cclache_envs()
    try:
        print subprocess.check_output([CCLASH_EXE, "--cclash-server"], env=envs)
    except subprocess.CalledProcessError as cpe:
        print cpe.output
        raise


def setup_module():
    """
    Before all tests
    :return:
    """
    assert os.path.isfile(CCLASH_EXE), "you need to build a Debug cclash first"
    print "cclash is at {}".format(CCLASH_EXE)

    tpo.get_vc_envs()
    tpo.download_openssl()
    setup_module.server = threading.Thread(target=run_server)
    setup_module.server.start()
setup_module.server = None


def teardown_module():
    """
    Clean up the server
    :return:
    """
    envs = setup_cclache_envs()
    subprocess.check_call([CCLASH_EXE, "--cclash", "--stop"], env=envs)


def setup_function(request):
    """
    Before each test
    :param request:
    :return:
    """
    envs = setup_cclache_envs()
    tpo.setup_function(request)
    print "cachedir {}".format(envs["CCLASH_DIR"])
    print subprocess.check_output([CCLASH_EXE, "--cclash"], env=envs)


def setup_cclache_envs():
    """
    return a dict of envs suitable for cclache to work with
    :return:
    """
    envs = dict(tpo.ENVS)
    cachedir = os.path.join(os.getcwd(), "cclache_cachedir")
    envs["CCLASH_DIR"] = cachedir
    envs["CCLASH_Z7_OBJ"] = "yes"
    envs["CCLASH_SERVER"] = "1"
    return envs


def test_build_nocache():
    """
    Time an openssl build with no caching involved at all
    :return:
    """
    tpo.build_openssl(None)


def build_withcclache_cold():
    """
    Time an openssl build with a cold cache
    :return:
    """
    envs = setup_cclache_envs()
    tpo.retry_delete(envs["CCLASH_DIR"])
    tpo.build_openssl(CCLASH_BIN, envs)


def test_build_withcclache_01_warm():
    """
    Time an openssl build with a warm cache
    :return:
    """
    #
    # Benchmarking on my win10 AMD A6-3500 (3 core).
    # On a good run this is 12.5 mins total,
    #
    # approx 450 sec cold
    # approx 120 sec warm
    #
    # overhead is non-compiler configure or clean time
    #
    envs = setup_cclache_envs()
    print "-" * 80
    print "Start cold cache"
    print "-" * 80
    build_withcclache_cold()

    tpo.setup_function(None)
    print "-" * 80
    print "Start warm cache"
    print "-" * 80
    tpo.build_openssl(CCLASH_BIN, envs)


if __name__ == "__main__":
    pytest.main(sys.argv[1:])

"""
Test cclash speed
"""
import sys
import os
import pytest

THISDIR = os.path.dirname(os.path.abspath(__file__))
CCLASH_BIN = os.path.join(os.path.dirname(THISDIR), "cclash", "cclash", "bin", "debug")

sys.path.append(os.path.join(THISDIR, "clcache", "speedtest"))
import test_performance_openssl as tpo


def setup_module():
    """
    Before all tests
    :return:
    """
    tpo.get_vc_envs()
    tpo.download_openssl()


def setup_function(request):
    """
    Before each test
    :param request:
    :return:
    """
    tpo.setup_function(request)


def setup_cclache_envs():
    """
    return a dict of envs suitable for cclache to work with
    :return:
    """
    envs = dict(tpo.ENVS)
    cachedir = os.path.join("clcache_cachedir")
    envs["CCLACHE_DIR"] = cachedir
    return envs


def test_build_nocache():
    """
    Time an openssl build with no caching involved at all
    :return:
    """
    tpo.build_openssl(None)


def test_build_withcclache_00_cold():
    """
    Time an openssl build with a cold cache
    :return:
    """
    envs = setup_cclache_envs()
    tpo.retry_delete(envs["CCLACHE_DIR"])
    tpo.build_openssl(CCLASH_BIN, envs)
    tpo.test_build_withcclache_00_cold.success = True
test_build_withcclache_00_cold.success = False


def test_build_withclcache_01_warm():
    """
    Time an openssl build with a warm cache
    :return:
    """
    assert test_build_withcclache_00_cold.success, "must run test_build_withcclache_00_cold first"
    envs = setup_cclache_envs()
    tpo.build_openssl(CCLASH_BIN, envs)



if __name__ == "__main__":
    pytest.main(sys.argv[1:])

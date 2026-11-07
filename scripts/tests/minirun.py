"""A stand-in runner for test files written in pytest's style, used because pytest is not installed on this
machine and could not be fetched (pip's TLS check failed). It provides pytest.raises, tmp_path and monkeypatch.
Its temporary files go under scratch/pytest-tmp/, never TEMP.

    python -B scripts/tests/minirun.py <test file> [-k substring]
"""
import importlib.util
import inspect
import shutil
import sys
import traceback
import types
from pathlib import Path

BASE = Path(__file__).resolve().parents[2] / "scratch" / "pytest-tmp"


class Raises:
    def __init__(self, exc):
        self.exc, self.value = exc, None

    def __enter__(self):
        return self

    def __exit__(self, kind, value, tb):
        if kind is None:
            raise AssertionError(f"did not raise {self.exc.__name__}")
        if issubclass(kind, self.exc):
            self.value = value
            return True
        return False


class Monkey:
    def __init__(self):
        self.undo = []

    def setattr(self, obj, name, value):
        self.undo.append((setattr, obj, name, getattr(obj, name)))
        setattr(obj, name, value)

    def setitem(self, d, key, value):
        self.undo.append((_restore_item, d, key, d.get(key, KeyError)))
        d[key] = value

    def finish(self):
        for fn, a, b, c in reversed(self.undo):
            fn(a, b, c)


def _restore_item(d, key, old):
    if old is KeyError:
        d.pop(key, None)
    else:
        d[key] = old


def main(argv):
    path = Path(argv[1]).resolve()
    only = argv[argv.index("-k") + 1] if "-k" in argv else ""
    pytest = types.ModuleType("pytest")
    pytest.raises = Raises
    sys.modules["pytest"] = pytest
    spec = importlib.util.spec_from_file_location(path.stem, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    shutil.rmtree(BASE, ignore_errors=True)
    failed = passed = 0
    for name, fn in sorted(vars(module).items(), key=lambda kv: getattr(kv[1], "__code__", None) and kv[1].__code__.co_firstlineno or 0):
        if not name.startswith("test_") or not callable(fn) or only not in name:
            continue
        tmp, mp = BASE / name, Monkey()
        tmp.mkdir(parents=True)
        wanted = {"tmp_path": tmp, "monkeypatch": mp}
        try:
            fn(**{p: wanted[p] for p in inspect.signature(fn).parameters})
            passed += 1
            print("PASS", name)
        except BaseException:
            failed += 1
            print("FAIL", name)
            traceback.print_exc()
        finally:
            mp.finish()
    print(f"{passed} passed, {failed} failed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

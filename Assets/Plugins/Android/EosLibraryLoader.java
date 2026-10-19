package com.mafianight.eos;

/**
 * Loads the EOS native library from the app's own class loader. Calling System.loadLibrary through JNI from C#
 * has no caller class, so Android searches the system namespace and does not find the app's library folder.
 */
public final class EosLibraryLoader {
    private EosLibraryLoader() {}

    public static void load() {
        System.loadLibrary("EOSSDK");
    }
}

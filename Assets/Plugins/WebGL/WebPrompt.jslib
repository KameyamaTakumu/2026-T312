mergeInto(LibraryManager.library, {

  // ブラウザの prompt を開き、入力された文字列を返す（キャンセルなら 0）
  WebPrompt_Show: function (messagePtr, defaultPtr) {
    var message = UTF8ToString(messagePtr);
    var def = UTF8ToString(defaultPtr);

    var result = window.prompt(message, def);
    if (result === null) return 0;

    var size = lengthBytesUTF8(result) + 1;
    var buffer = _malloc(size);
    stringToUTF8(result, buffer, size);
    return buffer;
  },

  WebPrompt_Free: function (ptr) {
    _free(ptr);
  }
});
CFLAGS+=-std=c99 -pedantic -Wall -Wextra -march=native -O3 -D_XOPEN_SOURCE -D_GNU_SOURCE -g
LDFLAGS+=-rdynamic
LDLIBS+=-lm
BINS=decoder encoder
BINDIR?=$(DESTDIR)$(PREFIX)/usr/bin

CFLAGS+=$(EXTRA_CFLAGS)
LDFLAGS+=$(EXTRA_LDFLAGS)
LDLIBS+=$(EXTRA_LDLIBS)

.PHONY: all
all: $(BINS)

.PHONY: clean
clean:
	$(RM) -- $(BINS) *.o

.PHONY: distclean
distclean: clean
	$(RM) -- *.gcda

decoder: decoder.o common.o io.o huffman.o coeffs.o imgproc.o frame.o

encoder: encoder.o common.o io.o huffman.o coeffs.o imgproc.o frame.o

.PHONY: install
install: all
	install -d $(BINDIR)
	install -m 755 $(BINS) $(BINDIR)

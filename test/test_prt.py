#!/usr/bin/python
import librsync
import tempfile

# get the file objects
dst = file('old.SLDPRT')
src = file('new.SLDPRT')
synced = tempfile.SpooledTemporaryFile(max_size=500000000,mode='w+b')

# do the librsync stuff
signature = librsync.signature(dst)
delta = librsync.delta(src, signature)
librsync.patch(dst,delta,synced)


# write the synced file
synced.seek(0)
syncedfile = open('synced.SLDPRT','wb')
buf = synced.read()
syncedfile.write(buf)
syncedfile.close()

# write the signature to file
signature.seek(0)
sigfile = open('prt.signature','wb')
buf = signature.read()
sigfile.write(buf)
sigfile.close()

# write the delta to file
delta.seek(0)
delfile = open('prt.delta','wb')
buf = delta.read()
delfile.write(buf)
delfile.close()


//! Adapter converting Windows IStream into standard Rust Read + Seek.

use std::io::{Read, Seek, SeekFrom};
use windows::Win32::System::Com::{IStream, STREAM_SEEK_CUR, STREAM_SEEK_END, STREAM_SEEK_SET};

pub struct StreamAdapter {
    stream: IStream,
}

impl StreamAdapter {
    pub fn new(stream: IStream) -> Self {
        Self { stream }
    }
}

impl Read for StreamAdapter {
    fn read(&mut self, buf: &mut [u8]) -> std::io::Result<usize> {
        if buf.is_empty() {
            return Ok(0);
        }

        let mut bytes_read: u32 = 0;
        let hr = unsafe {
            self.stream.Read(
                buf.as_mut_ptr() as *mut _,
                buf.len() as u32,
                Some(&mut bytes_read),
            )
        };

        if hr.is_err() {
            return Err(std::io::Error::new(
                std::io::ErrorKind::Other,
                format!("IStream::Read error: 0x{:08X}", hr.0),
            ));
        }

        Ok(bytes_read as usize)
    }
}

impl Seek for StreamAdapter {
    fn seek(&mut self, pos: SeekFrom) -> std::io::Result<u64> {
        let (origin, offset) = match pos {
            SeekFrom::Start(offset) => (STREAM_SEEK_SET, offset as i64),
            SeekFrom::Current(offset) => (STREAM_SEEK_CUR, offset),
            SeekFrom::End(offset) => (STREAM_SEEK_END, offset),
        };

        let mut new_pos: u64 = 0;
        let hr = unsafe {
            self.stream
                .Seek(offset, origin, Some(&mut new_pos))
        };

        if let Err(e) = hr {
            return Err(std::io::Error::new(
                std::io::ErrorKind::Other,
                format!("IStream::Seek error: {:?}", e),
            ));
        }

        Ok(new_pos)
    }
}

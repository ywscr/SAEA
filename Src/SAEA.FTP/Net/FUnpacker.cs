﻿/****************************************************************************
*项目名称：SAEA.FTP.Net
*CLR 版本：4.0.30319.42000
*机器名称：WALLE-PC
*命名空间：SAEA.FTP.Net
*类 名 称：FUnpacker
*版 本 号：V1.0.0.0
*创建人： yswenli
*电子邮箱：yswenli@outlook.com
*创建时间：2019/11/7 15:01:28
*描述：
*=====================================================================
*修改时间：2019/11/7 15:01:28
*修 改 人： yswenli
*版 本 号： V1.0.0.0
*描    述：
*****************************************************************************/
using SAEA.Sockets.Interface;
using System;

namespace SAEA.FTP.Net
{
    public class FUnpacker : IUnpacker
    {
        public void Clear()
        {
            throw new NotImplementedException();
        }

        public void Unpack(byte[] data, Action<ISocketProtocal> unpackCallback, Action<DateTime> onHeart = null, Action<byte[]> onFile = null)
        {
            
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rhino.Geometry;

namespace ghgl
{
    class GLSLViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        const double DefaultLineWidth = 3.0;
        const double DefaultPointSize = 8.0;

        readonly Shader[] _shaders = new Shader[(int)ShaderType.Fragment+1];
        bool _compileFailed;
        uint _programId;
        double _glLineWidth = DefaultLineWidth;
        double _glPointSize = DefaultPointSize;
        uint _drawMode;
        readonly DateTime _startTime = DateTime.Now;

        public GLSLViewModel()
        {
            for (int i = 0; i < (int)ShaderType.Fragment+1; i++)
            {
                _shaders[i] = new Shader((ShaderType)i, this);
                _shaders[i].PropertyChanged += OnShaderChanged;
            }
        }

        public bool Modified
        {
            get; set;
        }

        private void OnShaderChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Code")
            {
                Modified = true;
                ProgramId = 0;
            }
        }

        void SetCode(int which, string v, [System.Runtime.CompilerServices.CallerMemberName] string memberName = null)
        {
            if (!string.Equals(_shaders[which].Code, v, StringComparison.Ordinal))
            {
                _shaders[which].Code = v;
                OnPropertyChanged(memberName);
            }
        }

        public string TransformFeedbackShaderCode
        {
            get => _shaders[(int)ShaderType.TransformFeedbackVertex].Code;
            set => SetCode((int)ShaderType.TransformFeedbackVertex, value);
        }

        public string VertexShaderCode
        {
            get => _shaders[(int)ShaderType.Vertex].Code;
            set => SetCode((int)ShaderType.Vertex, value);
        }
        public string TessellationControlCode
        {
            get => _shaders[(int)ShaderType.TessellationControl].Code;
            set => SetCode((int)ShaderType.TessellationControl, value);
        }
        public string TessellationEvalualtionCode
        {
            get => _shaders[(int)ShaderType.TessellationEval].Code;
            set => SetCode((int)ShaderType.TessellationEval, value);
        }
        public string FragmentShaderCode
        {
            get => _shaders[(int)ShaderType.Fragment].Code;
            set => SetCode((int)ShaderType.Fragment, value);
        }
        public string GeometryShaderCode
        {
            get => _shaders[(int)ShaderType.Geometry].Code;
            set => SetCode((int)ShaderType.Geometry, value);
        }

        public Shader GetShader(ShaderType which)
        {
            return _shaders[(int)which];
        }

        public string GetCode(ShaderType type)
        {
            switch (type)
            {
                case ShaderType.TransformFeedbackVertex:
                    return TransformFeedbackShaderCode;
                case ShaderType.Vertex:
                    return VertexShaderCode;
                case ShaderType.Geometry:
                    return GeometryShaderCode;
                case ShaderType.TessellationControl:
                    return TessellationControlCode;
                case ShaderType.TessellationEval:
                    return TessellationEvalualtionCode;
                case ShaderType.Fragment:
                    return FragmentShaderCode;
            }
            return "";
        }

        public void SetCode(ShaderType type, string code)
        {
            switch (type)
            {
                case ShaderType.TransformFeedbackVertex:
                    TransformFeedbackShaderCode = code;
                    break;
                case ShaderType.Vertex:
                    VertexShaderCode = code;
                    break;
                case ShaderType.Geometry:
                    GeometryShaderCode = code;
                    break;
                case ShaderType.TessellationControl:
                    TessellationControlCode = code;
                    break;
                case ShaderType.TessellationEval:
                    TessellationEvalualtionCode = code;
                    break;
                case ShaderType.Fragment:
                    FragmentShaderCode = code;
                    break;
            }
        }

        public uint ProgramId
        {
            get { return _programId; }
            set
            {
                if (_programId != value)
                {
                    if(RecycleCurrentProgram)
                      GLRecycleBin.AddProgramToDeleteList(_programId);
                    _programId = value;
                    RecycleCurrentProgram = true;
                    OnPropertyChanged();
                }
            }
        }

        public bool RecycleCurrentProgram { get; set; } = true;

        public double glLineWidth
        {
            get { return _glLineWidth; }
            set
            {
                if (_glLineWidth != value && value > 0)
                {
                    _glLineWidth = value;
                    OnPropertyChanged();
                }
            }
        }

        public double glPointSize
        {
            get { return _glPointSize; }
            set
            {
                if (_glPointSize != value && value > 0)
                {
                    _glPointSize = value;
                    OnPropertyChanged();
                }
            }
        }

        public uint DrawMode
        {
            get { return _drawMode; }
            set
            {
                if (_drawMode != value && _drawMode <= OpenGL.GL_PATCHES)
                {
                    _drawMode = value;
                    OnPropertyChanged();
                }
            }
        }

        void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string memberName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(memberName));

            if (memberName == "VertexShaderCode" || memberName == "TessellationControlCode" || memberName == "TessellationEvalualtionCode"
              || memberName == "FragmentShaderCode" || memberName == "GeometryShaderCode")
            {
                ProgramId = 0;
                _compileFailed = false;
            }
        }
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        List<CompileError> _compileErrors = new List<CompileError>();
        public CompileError[] AllCompileErrors()
        {
            List<CompileError> errors = new List<CompileError>(_compileErrors);
            foreach (var shader in _shaders)
                errors.AddRange(shader.CompileErrors);
            return errors.ToArray();
        }

        public bool CompileProgram()
        {
            if (ProgramId != 0)
                return true;
            if (_compileFailed)
                return false;

            GLShaderComponentBase.ActivateGlContext();

            GLRecycleBin.Recycle();

            _compileErrors.Clear();
            bool compileSuccess = true;
            foreach (var shader in _shaders)
                compileSuccess = shader.Compile() && compileSuccess;

            // we want to make sure that at least a vertex and fragment shader
            // exist before making a program
            if (string.IsNullOrWhiteSpace(_shaders[(int)ShaderType.Vertex].Code))
            {
                _compileErrors.Add(new CompileError("A vertex shader is required to create a GL program"));
                compileSuccess = false;
            }
            if (string.IsNullOrWhiteSpace(_shaders[(int)ShaderType.Fragment].Code))
            {
                _compileErrors.Add(new CompileError("A fragment shader is required to create a GL program"));
                compileSuccess = false;
            }

            if (compileSuccess)
            {
                ProgramId = OpenGL.glCreateProgram();
                foreach (var shader in _shaders)
                {
                    if (shader.ShaderId != 0)
                        OpenGL.glAttachShader(ProgramId, shader.ShaderId);
                }

                OpenGL.glLinkProgram(ProgramId);

                string errorMsg;
                if (OpenGL.ErrorOccurred(out errorMsg))
                {
                    OpenGL.glDeleteProgram(_programId);
                    ProgramId = 0;
                    _compileErrors.Add(new CompileError(errorMsg));
                }
            }
            _compileFailed = (ProgramId == 0);
            return ProgramId != 0;
        }

        public bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            writer.SetString("VertexShader", VertexShaderCode);
            writer.SetString("GeometryShader", GeometryShaderCode);
            writer.SetString("FragmentShader", FragmentShaderCode);
            writer.SetString("TessCtrlShader", TessellationControlCode);
            writer.SetString("TessEvalShader", TessellationEvalualtionCode);
            writer.SetString("TransformFeedbackVertexShader", TransformFeedbackShaderCode);
            writer.SetDouble("glLineWidth", glLineWidth);
            writer.SetDouble("glPointSize", glPointSize);
            writer.SetInt32("DrawMode", (int)DrawMode);
            return true;
        }

        public bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            string s = "";
            VertexShaderCode = reader.TryGetString("VertexShader", ref s) ? s : "";
            GeometryShaderCode = reader.TryGetString("GeometryShader", ref s) ? s : "";
            FragmentShaderCode = reader.TryGetString("FragmentShader", ref s) ? s : "";
            TessellationControlCode = reader.TryGetString("TessCtrlShader", ref s) ? s : "";
            TessellationEvalualtionCode = reader.TryGetString("TessEvalShader", ref s) ? s : "";
            TransformFeedbackShaderCode = reader.TryGetString("TransformFeedbackVertexShader", ref s) ? s : "";
            double d = 0;
            if (reader.TryGetDouble("glLineWidth", ref d))
                glLineWidth = d;
            if (reader.TryGetDouble("glPointSize", ref d))
                glPointSize = d;
            int i = 0;
            if (reader.TryGetInt32("DrawMode", ref i))
                DrawMode = (uint)i;
            return true;
        }

        /// <summary>
        /// Get the data type for a uniform in this program (all shaders)
        /// </summary>
        /// <param name="name">name of uniform to try and get a type for</param>
        /// <param name="dataType"></param>
        /// <returns></returns>
        public bool TryGetUniformType(string name, out string dataType)
        {
            dataType = "";
            foreach (var shader in _shaders)
            {
                var uniforms = shader.GetUniforms();
                foreach (UniformDescription uni in uniforms)
                {
                    if (uni.Name == name)
                    {
                        dataType = uni.DataType;
                        return true;
                    }
                }
            }
            return false;
        }

        public bool TryGetAttributeType(string name, out string dataType, out int location)
        {
            dataType = "";
            location = -1;
            foreach (var shader in _shaders)
            {
                var attributes = shader.GetVertexAttributes();
                foreach (AttributeDescription attrib in attributes)
                {
                    if (attrib.Name == name)
                    {
                        dataType = attrib.DataType;
                        location = attrib.Location;
                        return true;
                    }
                }
            }
            return false;
        }


        class UniformData<T>
        {
            public UniformData(string name, T[] value)
            {
                Name = name;
                Data = value;
            }

            public string Name { get; private set; }
            public T[] Data { get; private set; }
        }

        class SamplerUniformData
        {
            uint _textureId;

            public SamplerUniformData(string name, string path)
            {
                Name = name;
                Path = path;
            }
            public string Name { get; private set; }
            public String Path { get; private set; }

            public uint TextureId
            {
                get { return _textureId; }
                set
                {
                    if (_textureId != value)
                    {
                        GLRecycleBin.AddTextureToDeleteList(_textureId);
                        _textureId = value;
                    }
                }
            }
            public static uint CreateTexture(string path)
            {
                uint textureId;
                try
                {
                    using (var bmp = new System.Drawing.Bitmap(path))
                    {
                        var rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
                        uint[] textures = { 0 };
                        OpenGL.glGenTextures(1, textures);
                        OpenGL.glBindTexture(OpenGL.GL_TEXTURE_2D, textures[0]);

                        if (bmp.PixelFormat == System.Drawing.Imaging.PixelFormat.Format24bppRgb)
                        {
                            var bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                            OpenGL.glTexImage2D(OpenGL.GL_TEXTURE_2D, 0, (int)OpenGL.GL_RGB, bmpData.Width, bmpData.Height, 0, OpenGL.GL_BGR, OpenGL.GL_UNSIGNED_BYTE, bmpData.Scan0);
                            bmp.UnlockBits(bmpData);
                        }
                        else
                        {
                            var bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                            OpenGL.glTexImage2D(OpenGL.GL_TEXTURE_2D, 0, (int)OpenGL.GL_RGBA, bmpData.Width, bmpData.Height, 0, OpenGL.GL_BGRA, OpenGL.GL_UNSIGNED_BYTE, bmpData.Scan0);
                            bmp.UnlockBits(bmpData);
                        }
                        textureId = textures[0];
                        OpenGL.glGenerateMipmap(OpenGL.GL_TEXTURE_2D);
                        OpenGL.glTexParameteri(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, (int)OpenGL.GL_REPEAT);
                        OpenGL.glTexParameteri(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, (int)OpenGL.GL_REPEAT);
                        OpenGL.glTexParameteri(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, (int)OpenGL.GL_LINEAR_MIPMAP_LINEAR);
                        OpenGL.glTexParameteri(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, (int)OpenGL.GL_LINEAR_MIPMAP_LINEAR);
                        OpenGL.glBindTexture(OpenGL.GL_TEXTURE_2D, 0);
                    }
                }
                catch (Exception)
                {
                    textureId = 0;
                }

                return textureId;
            }
        }

        class MeshData
        {
            uint _triangleIndexBuffer;
            uint _linesIndexBuffer;
            uint _vertexVbo;
            uint _normalVbo;
            uint _textureCoordVbo;
            public MeshData(Mesh mesh)
            {
                Mesh = mesh;
            }
            public Mesh Mesh { get; private set; }

            public uint TriangleIndexBuffer
            {
                get { return _triangleIndexBuffer; }
                set
                {
                    if(_triangleIndexBuffer!=value)
                    {
                        GLRecycleBin.AddVboToDeleteList(_triangleIndexBuffer);
                        _triangleIndexBuffer = value;
                    }
                }
            }

            public uint LinesIndexBuffer
            {
                get { return _linesIndexBuffer; }
                set
                {
                    if (_linesIndexBuffer != value)
                    {
                        GLRecycleBin.AddVboToDeleteList(_linesIndexBuffer);
                        _linesIndexBuffer = value;
                    }
                }
            }

            public uint VertexVbo
            {
                get { return _vertexVbo; }
                set
                {
                    if(_vertexVbo!=value)
                    {
                        GLRecycleBin.AddVboToDeleteList(_vertexVbo);
                        _vertexVbo = value;
                    }
                }
            }

            public uint NormalVbo
            {
                get { return _normalVbo; }
                set
                {
                    if(_normalVbo!=value)
                    {
                        GLRecycleBin.AddVboToDeleteList(_normalVbo);
                        _normalVbo = value;
                    }
                }
            }

            public uint TextureCoordVbo
            {
                get { return _textureCoordVbo; }
                set
                {
                    if(_textureCoordVbo!=value)
                    {
                        GLRecycleBin.AddVboToDeleteList(_textureCoordVbo);
                        _textureCoordVbo = value;
                    }
                }
            }
        }

        readonly List<MeshData> _meshes = new List<MeshData>();
        readonly List<UniformData<int>> _intUniforms = new List<UniformData<int>>();
        readonly List<UniformData<float>> _floatUniforms = new List<UniformData<float>>();
        readonly List<UniformData<Point3f>> _vec3Uniforms = new List<UniformData<Point3f>>();
        readonly List<UniformData<Vec4>> _vec4Uniforms = new List<UniformData<Vec4>>();
        readonly List<SamplerUniformData> _sampler2DUniforms = new List<SamplerUniformData>();

        public void AddMesh(Mesh mesh)
        {
            _meshes.Add(new MeshData(mesh));
        }

        public void AddUniform(string name, int[] values)
        {
            _intUniforms.Add(new UniformData<int>(name, values));
        }
        public void AddUniform(string name, float[] values)
        {
            _floatUniforms.Add(new UniformData<float>(name, values));
        }
        public void AddUniform(string name, Point3f[] values)
        {
            _vec3Uniforms.Add(new UniformData<Point3f>(name, values));
        }
        public void AddUniform(string name, Vec4[] values)
        {
            _vec4Uniforms.Add(new UniformData<Vec4>(name, values));
        }
        public void AddSampler2DUniform(string name, string path)
        {
            var data = new SamplerUniformData(name, path);
            //try to find a cached item first
            for (int i = 0; i < samplerCache.Count; i++)
            {
                var sampler = samplerCache[i];
                if (string.Equals(sampler.Path, path, StringComparison.OrdinalIgnoreCase))
                {
                    data.TextureId = sampler.TextureId;
                    samplerCache.RemoveAt(i);
                    break;
                }
            }
            _sampler2DUniforms.Add(data);
        }

        public void AddAttribute(string name, int location, int[] value)
        {
            _intAttribs.Add(new GLAttribute<int>(name, location, value));
        }
        public void AddAttribute(string name, int location, float[] value)
        {
            _floatAttribs.Add(new GLAttribute<float>(name, location, value));
        }
        public void AddAttribute(string name, int location, Point3f[] value)
        {
            _vec3Attribs.Add(new GLAttribute<Point3f>(name, location, value));
        }
        public void AddAttribute(string name, int location, Vec4[] value)
        {
            _vec4Attribs.Add(new GLAttribute<Vec4>(name, location, value));
        }

        static int UniformLocation(uint programId, string name, out int arrayLength)
        {
            arrayLength = 0;
            int index = name.IndexOf("[", StringComparison.Ordinal);
            int index2 = index < 0 ? -1 : name.IndexOf("]", index, StringComparison.Ordinal);
            if (index > 0 && index2>index)
            {
                string count = name.Substring(index + 1, index2 - index - 1);
                int.TryParse(count, out arrayLength);
                name = name.Substring(0, index);
            }
            return OpenGL.glGetUniformLocation(programId, name);
        }

        void SetupGLUniforms()
        {
            foreach (var uniform in _intUniforms)
            {
                int arrayLength;
                int location = UniformLocation(ProgramId, uniform.Name, out arrayLength);
                if (-1 != location)
                {
                    if (arrayLength < 1)
                        OpenGL.glUniform1i(location, uniform.Data[0]);
                    else if (uniform.Data.Length >= arrayLength)
                        OpenGL.glUniform1iv(location, arrayLength, uniform.Data);
                }
            }
            foreach (var uniform in _floatUniforms)
            {
                int arrayLength;
                int location = UniformLocation(ProgramId, uniform.Name, out arrayLength);
                if (-1 != location)
                {
                    if (arrayLength < 1)
                        OpenGL.glUniform1f(location, uniform.Data[0]);
                    else if (uniform.Data.Length >= arrayLength)
                        OpenGL.glUniform1fv(location, arrayLength, uniform.Data);
                }
            }
            foreach (var uniform in _vec3Uniforms)
            {
                int arrayLength;
                int location = UniformLocation(ProgramId, uniform.Name, out arrayLength);
                if (-1 != location)
                {
                    if (arrayLength < 1)
                        OpenGL.glUniform3f(location, uniform.Data[0].X, uniform.Data[0].Y, uniform.Data[0].Z);
                    else if (uniform.Data.Length >= arrayLength)
                    {
                        float[] data = new float[arrayLength * 3];
                        for(int i=0; i<arrayLength; i++)
                        {
                            data[i * 3] = uniform.Data[i].X;
                            data[i * 3 + 1] = uniform.Data[i].Y;
                            data[i * 3 + 2] = uniform.Data[i].Z;
                        }
                        OpenGL.glUniform3fv(location, arrayLength, data);
                    }
                }
            }
            foreach (var uniform in _vec4Uniforms)
            {
                int arrayLength;
                int location = UniformLocation(ProgramId, uniform.Name, out arrayLength);
                if (-1 != location)
                {
                    if (arrayLength < 1)
                        OpenGL.glUniform4f(location, uniform.Data[0]._x, uniform.Data[0]._y, uniform.Data[0]._z, uniform.Data[0]._w);
                    else if (uniform.Data.Length >= arrayLength)
                    {
                        float[] data = new float[arrayLength * 4];
                        for (int i = 0; i < arrayLength; i++)
                        {
                            data[i * 4] = uniform.Data[i]._x;
                            data[i * 4 + 1] = uniform.Data[i]._y;
                            data[i * 4 + 2] = uniform.Data[i]._z;
                            data[i * 4 + 3] = uniform.Data[i]._w;
                        }
                        OpenGL.glUniform4fv(location, arrayLength, data);
                    }
                }
            }

            int currentTexture = 0;
            foreach (var uniform in _sampler2DUniforms)
            {
                int location = OpenGL.glGetUniformLocation(ProgramId, uniform.Name);
                if (-1 != location)
                {
                    if (0 == uniform.TextureId)
                    {
                        uniform.TextureId = SamplerUniformData.CreateTexture(uniform.Path);
                    }
                    if (uniform.TextureId != 0)
                    {
                        OpenGL.glActiveTexture(OpenGL.GL_TEXTURE0 + (uint)currentTexture);
                        OpenGL.glBindTexture(OpenGL.GL_TEXTURE_2D, uniform.TextureId);
                        OpenGL.glUniform1i(location, currentTexture);
                        currentTexture++;
                    }
                }
            }
        }

        int SetupGLAttributes(int index)
        {
            int element_count = 0;
            if (_meshes.Count >= (index + 1))
            {
                var data = _meshes[index];
                var mesh = data.Mesh;
                element_count = mesh.Vertices.Count;
                int location = OpenGL.glGetAttribLocation(ProgramId, "_meshVertex");
                if(location>=0)
                {
                    if (data.VertexVbo == 0)
                    {
                        uint[] buffers;
                        OpenGL.glGenBuffers(1, out buffers);
                        data.VertexVbo = buffers[0];
                        OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, data.VertexVbo);
                        IntPtr size = new IntPtr(3 * sizeof(float) * mesh.Vertices.Count);
                        var points = mesh.Vertices.ToPoint3fArray();
                        var handle = GCHandle.Alloc(points, GCHandleType.Pinned);
                        IntPtr pointer = handle.AddrOfPinnedObject();
                        OpenGL.glBufferData(OpenGL.GL_ARRAY_BUFFER, size, pointer, OpenGL.GL_STREAM_DRAW);
                        handle.Free();
                    }
                    OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, data.VertexVbo);
                    OpenGL.glEnableVertexAttribArray((uint)location);
                    OpenGL.glVertexAttribPointer((uint)location, 3, OpenGL.GL_FLOAT, 0, 0, IntPtr.Zero);
                }
                location = OpenGL.glGetAttribLocation(ProgramId, "_meshNormal");
                if (location >= 0)
                {
                    if (data.NormalVbo == 0 && mesh.Normals.Count == mesh.Vertices.Count)
                    {
                        uint[] buffers;
                        OpenGL.glGenBuffers(1, out buffers);
                        data.NormalVbo = buffers[0];
                        OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, data.NormalVbo);
                        IntPtr size = new IntPtr(3 * sizeof(float) * mesh.Normals.Count);
                        var normals = mesh.Normals.ToFloatArray();
                        var handle = GCHandle.Alloc(normals, GCHandleType.Pinned);
                        IntPtr pointer = handle.AddrOfPinnedObject();
                        OpenGL.glBufferData(OpenGL.GL_ARRAY_BUFFER, size, pointer, OpenGL.GL_STREAM_DRAW);
                        handle.Free();
                    }
                    if (data.NormalVbo != 0)
                    {
                        OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, data.NormalVbo);
                        OpenGL.glEnableVertexAttribArray((uint)location);
                        OpenGL.glVertexAttribPointer((uint)location, 3, OpenGL.GL_FLOAT, 0, 0, IntPtr.Zero);
                    }
                    else
                    {
                        OpenGL.glDisableVertexAttribArray((uint)location);
                        OpenGL.glVertexAttrib3f((uint)location, 0, 0, 0);
                    }
                }

                location = OpenGL.glGetAttribLocation(ProgramId, "_meshTextureCoordinate");
                if (location >= 0)
                {
                    if (data.TextureCoordVbo == 0 && mesh.TextureCoordinates.Count == mesh.Vertices.Count)
                    {
                        uint[] buffers;
                        OpenGL.glGenBuffers(1, out buffers);
                        data.TextureCoordVbo = buffers[0];
                        OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, data.TextureCoordVbo);
                        IntPtr size = new IntPtr(2 * sizeof(float) * mesh.TextureCoordinates.Count);
                        var tcs = mesh.TextureCoordinates.ToFloatArray();
                        var handle = GCHandle.Alloc(tcs, GCHandleType.Pinned);
                        IntPtr pointer = handle.AddrOfPinnedObject();
                        OpenGL.glBufferData(OpenGL.GL_ARRAY_BUFFER, size, pointer, OpenGL.GL_STREAM_DRAW);
                        handle.Free();
                    }
                    if (data.TextureCoordVbo != 0)
                    {
                        OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, data.TextureCoordVbo);
                        OpenGL.glEnableVertexAttribArray((uint)location);
                        OpenGL.glVertexAttribPointer((uint)location, 2, OpenGL.GL_FLOAT, 0, 0, IntPtr.Zero);
                    }
                    else
                    {
                        OpenGL.glDisableVertexAttribArray((uint)location);
                        OpenGL.glVertexAttrib2f((uint)location, 0, 0);
                    }
                }

            }

            foreach (var item in _intAttribs)
            {
                if (element_count == 0)
                    element_count = item.Items.Length;
                if (element_count > item.Items.Length && item.Items.Length > 1)
                    element_count = item.Items.Length;

                if (item.Location < 0)
                {
                    item.Location = OpenGL.glGetAttribLocation(ProgramId, item.Name);
                }
                if (item.Location >= 0)
                {
                    uint location = (uint)item.Location;
                    if (1 == item.Items.Length)
                    {
                        OpenGL.glDisableVertexAttribArray(location);
                        OpenGL.glVertexAttribI1i(location, item.Items[0]);
                    }
                    else
                    {
                        if (item.VboHandle == 0)
                        {
                            uint[] buffers;
                            OpenGL.glGenBuffers(1, out buffers);
                            item.VboHandle = buffers[0];
                            OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, item.VboHandle);
                            IntPtr size = new IntPtr(sizeof(int) * item.Items.Length);
                            var handle = GCHandle.Alloc(item.Items, GCHandleType.Pinned);
                            IntPtr pointer = handle.AddrOfPinnedObject();
                            OpenGL.glBufferData(OpenGL.GL_ARRAY_BUFFER, size, pointer, OpenGL.GL_STREAM_DRAW);
                            handle.Free();
                        }
                        OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, item.VboHandle);
                        OpenGL.glEnableVertexAttribArray(location);
                        OpenGL.glVertexAttribPointer(location, 1, OpenGL.GL_INT, 0, sizeof(int), IntPtr.Zero);
                    }
                }
            }
            foreach (var item in _floatAttribs)
            {
                if (element_count == 0)
                    element_count = item.Items.Length;
                if (element_count > item.Items.Length && item.Items.Length > 1)
                    element_count = item.Items.Length;

                if (item.Location < 0)
                {
                    item.Location = OpenGL.glGetAttribLocation(ProgramId, item.Name);
                }
                if (item.Location >= 0)
                {
                    uint location = (uint)item.Location;
                    if (1 == item.Items.Length)
                    {
                        OpenGL.glDisableVertexAttribArray(location);
                        OpenGL.glVertexAttrib1f(location, item.Items[0]);
                    }
                    else
                    {
                        if (item.VboHandle == 0)
                        {
                            uint[] buffers;
                            OpenGL.glGenBuffers(1, out buffers);
                            item.VboHandle = buffers[0];
                            OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, item.VboHandle);
                            IntPtr size = new IntPtr(sizeof(float) * item.Items.Length);
                            var handle = GCHandle.Alloc(item.Items, GCHandleType.Pinned);
                            IntPtr pointer = handle.AddrOfPinnedObject();
                            OpenGL.glBufferData(OpenGL.GL_ARRAY_BUFFER, size, pointer, OpenGL.GL_STREAM_DRAW);
                            handle.Free();
                        }
                        OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, item.VboHandle);
                        OpenGL.glEnableVertexAttribArray(location);
                        OpenGL.glVertexAttribPointer(location, 1, OpenGL.GL_FLOAT, 0, sizeof(float), IntPtr.Zero);
                    }
                }
            }
            foreach (var item in _vec3Attribs)
            {
                if (element_count == 0)
                    element_count = item.Items.Length;
                if (element_count > item.Items.Length && item.Items.Length > 1)
                    element_count = item.Items.Length;

                if (item.Location < 0)
                {
                    item.Location = OpenGL.glGetAttribLocation(ProgramId, item.Name);
                }
                if (item.Location >= 0)
                {
                    uint location = (uint)item.Location;
                    if (1 == item.Items.Length)
                    {
                        OpenGL.glDisableVertexAttribArray(location);
                        Point3f v = item.Items[0];
                        OpenGL.glVertexAttrib3f(location, v.X, v.Y, v.Z);
                    }
                    else
                    {
                        if (item.VboHandle == 0)
                        {
                            uint[] buffers;
                            OpenGL.glGenBuffers(1, out buffers);
                            item.VboHandle = buffers[0];
                            OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, item.VboHandle);
                            IntPtr size = new IntPtr(3 * sizeof(float) * item.Items.Length);
                            var handle = GCHandle.Alloc(item.Items, GCHandleType.Pinned);
                            IntPtr pointer = handle.AddrOfPinnedObject();
                            OpenGL.glBufferData(OpenGL.GL_ARRAY_BUFFER, size, pointer, OpenGL.GL_STREAM_DRAW);
                            handle.Free();
                        }
                        OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, item.VboHandle);
                        OpenGL.glEnableVertexAttribArray(location);
                        OpenGL.glVertexAttribPointer(location, 3, OpenGL.GL_FLOAT, 0, 3 * sizeof(float), IntPtr.Zero);
                    }
                }
            }
            foreach (var item in _vec4Attribs)
            {
                if (element_count == 0)
                    element_count = item.Items.Length;
                if (element_count > item.Items.Length && item.Items.Length > 1)
                    element_count = item.Items.Length;

                if (item.Location < 0)
                {
                    item.Location = OpenGL.glGetAttribLocation(ProgramId, item.Name);
                }
                if (item.Location >= 0)
                {
                    uint location = (uint)item.Location;
                    if (1 == item.Items.Length)
                    {
                        OpenGL.glDisableVertexAttribArray(location);
                        Vec4 v = item.Items[0];
                        OpenGL.glVertexAttrib4f(location, v._x, v._y, v._z, v._w);
                    }
                    else
                    {
                        if (item.VboHandle == 0)
                        {
                            uint[] buffers;
                            OpenGL.glGenBuffers(1, out buffers);
                            item.VboHandle = buffers[0];
                            OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, item.VboHandle);
                            IntPtr size = new IntPtr(4 * sizeof(float) * item.Items.Length);
                            var handle = GCHandle.Alloc(item.Items, GCHandleType.Pinned);
                            IntPtr pointer = handle.AddrOfPinnedObject();
                            OpenGL.glBufferData(OpenGL.GL_ARRAY_BUFFER, size, pointer, OpenGL.GL_STREAM_DRAW);
                            handle.Free();
                        }
                        OpenGL.glBindBuffer(OpenGL.GL_ARRAY_BUFFER, item.VboHandle);
                        OpenGL.glEnableVertexAttribArray(location);
                        OpenGL.glVertexAttribPointer(location, 4, OpenGL.GL_FLOAT, 0, 4 * sizeof(float), IntPtr.Zero);
                    }
                }
            }
            return element_count;
        }

        readonly List<GLAttribute<int>> _intAttribs = new List<GLAttribute<int>>();
        readonly List<GLAttribute<float>> _floatAttribs = new List<GLAttribute<float>>();
        readonly List<GLAttribute<Point3f>> _vec3Attribs = new List<GLAttribute<Point3f>>();
        readonly List<GLAttribute<Vec4>> _vec4Attribs = new List<GLAttribute<Vec4>>();

        readonly List<SamplerUniformData> samplerCache = new List<SamplerUniformData>();

        public void ClearData()
        {
            foreach(var data in _meshes)
            {
                data.TriangleIndexBuffer = 0;
                data.LinesIndexBuffer = 0;
                data.NormalVbo = 0;
                data.TextureCoordVbo = 0;
                data.VertexVbo = 0;
            }
            _meshes.Clear();
            _intUniforms.Clear();
            _floatUniforms.Clear();
            _vec3Uniforms.Clear();
            _vec4Uniforms.Clear();

            foreach (var attr in _intAttribs)
                GLRecycleBin.AddVboToDeleteList(attr.VboHandle);
            _intAttribs.Clear();
            foreach (var attr in _floatAttribs)
                GLRecycleBin.AddVboToDeleteList(attr.VboHandle);
            _floatAttribs.Clear();
            foreach (var attr in _vec3Attribs)
                GLRecycleBin.AddVboToDeleteList(attr.VboHandle);
            _vec3Attribs.Clear();
            foreach (var attr in _vec4Attribs)
                GLRecycleBin.AddVboToDeleteList(attr.VboHandle);
            _vec4Attribs.Clear();

            samplerCache.AddRange(_sampler2DUniforms);
            while (samplerCache.Count > 10)
            {
                var sampler = samplerCache[0];
                GLRecycleBin.AddTextureToDeleteList(sampler.TextureId);
                samplerCache.RemoveAt(0);
            }
            _sampler2DUniforms.Clear();
        }

        public void Draw(Rhino.Display.DisplayPipeline display)
        {
            uint programId = ProgramId;
            if (programId == 0)
                return;

            uint[] vao;
            OpenGL.glGenVertexArrays(1, out vao);
            OpenGL.glBindVertexArray(vao[0]);
            OpenGL.glUseProgram(programId);

            SetupGLUniforms();

            // TODO: Parse shader and figure out the proper number to place here
            if (OpenGL.GL_PATCHES == DrawMode)
                OpenGL.glPatchParameteri(OpenGL.GL_PATCH_VERTICES, 1);

            float linewidth = (float)glLineWidth;
            OpenGL.glLineWidth(linewidth);
            float pointsize = (float)glPointSize;
            OpenGL.glPointSize(pointsize);

            // Define standard uniforms
            foreach(var builtin in BuiltIn.GetUniformBuiltIns())
                builtin.Setup(programId, display);

            if (OpenGL.GL_POINTS == DrawMode)
                OpenGL.glEnable(OpenGL.GL_VERTEX_PROGRAM_POINT_SIZE);
            OpenGL.glEnable(OpenGL.GL_BLEND);
            OpenGL.glBlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);

            int totalCount = 1;
            if (_meshes != null && _meshes.Count > 1)
                totalCount = _meshes.Count;

            for (int i = 0; i < totalCount; i++)
            {
                int element_count = SetupGLAttributes(i);
                if (element_count < 1)
                    continue;

                if( _meshes.Count>i )
                {
                    var data = _meshes[i];
                    if (DrawMode == OpenGL.GL_LINES && data.LinesIndexBuffer==0)
                    {
                        uint[] buffers;
                        OpenGL.glGenBuffers(1, out buffers);
                        data.LinesIndexBuffer = buffers[0];
                        OpenGL.glBindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, data.LinesIndexBuffer);

                        int[] indices = new int[ 6 * data.Mesh.Faces.TriangleCount + 8 * data.Mesh.Faces.QuadCount];
                        int current = 0;
                        foreach (var face in data.Mesh.Faces)
                        {
                            indices[current++] = face.A;
                            indices[current++] = face.B;
                            indices[current++] = face.B;
                            indices[current++] = face.C;
                            if (face.IsTriangle)
                            {
                                indices[current++] = face.C;
                                indices[current++] = face.A;
                            }
                            if (face.IsQuad)
                            {
                                indices[current++] = face.C;
                                indices[current++] = face.D;
                                indices[current++] = face.D;
                                indices[current++] = face.A;
                            }
                        }

                        var handle = GCHandle.Alloc(indices, GCHandleType.Pinned);
                        IntPtr pointer = handle.AddrOfPinnedObject();
                        IntPtr size = new IntPtr(sizeof(int) * indices.Length);
                        OpenGL.glBufferData(OpenGL.GL_ELEMENT_ARRAY_BUFFER, size, pointer, OpenGL.GL_STATIC_DRAW);
                        handle.Free();
                    }

                    if (DrawMode != OpenGL.GL_LINES && data.TriangleIndexBuffer==0)
                    {
                        uint[] buffers;
                        OpenGL.glGenBuffers(1, out buffers);
                        data.TriangleIndexBuffer = buffers[0];
                        OpenGL.glBindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, data.TriangleIndexBuffer);
                        int[] indices = new int[3 * (data.Mesh.Faces.TriangleCount + 2 * data.Mesh.Faces.QuadCount)];
                        int current = 0;
                        foreach(var face in data.Mesh.Faces)
                        {
                            indices[current++] = face.A;
                            indices[current++] = face.B;
                            indices[current++] = face.C;
                            if( face.IsQuad )
                            {
                                indices[current++] = face.C;
                                indices[current++] = face.D;
                                indices[current++] = face.A;
                            }
                        }

                        var handle = GCHandle.Alloc(indices, GCHandleType.Pinned);
                        IntPtr pointer = handle.AddrOfPinnedObject();
                        IntPtr size = new IntPtr(sizeof(int) * indices.Length);
                        OpenGL.glBufferData(OpenGL.GL_ELEMENT_ARRAY_BUFFER, size, pointer, OpenGL.GL_STATIC_DRAW);
                        handle.Free();
                    }

                    if (DrawMode == OpenGL.GL_LINES && data.LinesIndexBuffer != 0)
                    {
                        OpenGL.glBindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, data.LinesIndexBuffer);
                        int indexCount = 6 * data.Mesh.Faces.TriangleCount + 8 * data.Mesh.Faces.QuadCount;
                        OpenGL.glDrawElements(DrawMode, indexCount, OpenGL.GL_UNSIGNED_INT, IntPtr.Zero);
                        OpenGL.glBindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, 0);
                    }
                    if (DrawMode != OpenGL.GL_LINES && data.TriangleIndexBuffer != 0)
                    {
                        OpenGL.glBindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, data.TriangleIndexBuffer);
                        int indexCount = 3*(data.Mesh.Faces.TriangleCount + 2 * data.Mesh.Faces.QuadCount);
                        OpenGL.glDrawElements(DrawMode, indexCount, OpenGL.GL_UNSIGNED_INT, IntPtr.Zero);
                        OpenGL.glBindBuffer(OpenGL.GL_ELEMENT_ARRAY_BUFFER, 0);
                    }
                }
                else
                    OpenGL.glDrawArrays(DrawMode, 0, element_count);
            }
            foreach (var item in _intAttribs)
                DisableVertexAttribArray(item.Location);
            foreach (var item in _floatAttribs)
                DisableVertexAttribArray(item.Location);
            foreach (var item in _vec3Attribs)
                DisableVertexAttribArray(item.Location);
            foreach (var item in _vec4Attribs)
                DisableVertexAttribArray(item.Location);

            OpenGL.glBindVertexArray(0);
            OpenGL.glDeleteVertexArrays(1, vao);
            OpenGL.glUseProgram(0);
        }

        static void DisableVertexAttribArray(int location)
        {
            if (location >= 0)
                OpenGL.glDisableVertexAttribArray((uint)location);
        }

        public void SaveAs(string filename)
        {
            var text = new System.Text.StringBuilder();
            if( !string.IsNullOrWhiteSpace(TransformFeedbackShaderCode))
            {
                text.AppendLine("[transformfeedback vertex shader]");
                text.AppendLine(TransformFeedbackShaderCode);
            }

            text.AppendLine("[vertex shader]");
            text.AppendLine(VertexShaderCode);
            if( !string.IsNullOrWhiteSpace(GeometryShaderCode) )
            {
                text.AppendLine("[geometry shader]");
                text.AppendLine(GeometryShaderCode);
            }
            if( !string.IsNullOrWhiteSpace(TessellationControlCode))
            {
                text.AppendLine("[tessctrl shader]");
                text.AppendLine(TessellationControlCode);
            }
            if( !string.IsNullOrWhiteSpace(TessellationEvalualtionCode))
            {
                text.AppendLine("[tesseval shader]");
                text.AppendLine(TessellationEvalualtionCode);
            }
            text.AppendLine("[fragment shader]");
            text.AppendLine(FragmentShaderCode);
            System.IO.File.WriteAllText(filename, text.ToString());
        }
    }
}
